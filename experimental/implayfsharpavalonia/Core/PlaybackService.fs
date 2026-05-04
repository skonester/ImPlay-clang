namespace ImPlay.Core.Services

open System
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open Microsoft.FSharp.NativeInterop
open ImPlay.Core
open ImPlay.Core.Models

#nowarn "9" // Allow unsafe code

type DlnaCastDevice() =
    member val Name : string = "" with get, set
    member val Id : string = "" with get, set

[<AllowNullLiteral>]
type PlaybackService() as self =
    let mutable _mpv = MpvNative.mpv_create()
    let mutable _disposed = false
    let mutable _loop = false
    let mutable _pause = true
    let mutable _trackRevision = 0L
    let mutable _state = MediaState()
    let _stateLock = obj()
    
    let mutable _renderContext = IntPtr.Zero
    let mutable _renderCallback : MpvRenderUpdateCallback option = None
    let mutable _requestRender : Action option = None
    let mutable _getProcAddressDelegate : MpvOpenGlGetProcAddress option = None
    let mutable _rendererInitialized = false
    
    let mutable _eventThread : Thread option = None
    
    let mutable _initializationError : string option = None
    
    let videoFrameCaptured = Event<EventHandler<VideoFrameData>, VideoFrameData>()
    let stateChanged = Event<EventHandler<MediaState>, MediaState>()
    let endReached = Event<EventHandler, EventArgs>()
    let errorOccurred = Event<EventHandler<string>, string>()

    let stringToUtf8 (s: string) =
        let bytes = System.Text.Encoding.UTF8.GetBytes(s)
        let ptr = Marshal.AllocHGlobal(bytes.Length + 1)
        Marshal.Copy(bytes, 0, ptr, bytes.Length)
        Marshal.WriteByte(ptr, bytes.Length, 0uy)
        ptr

    let setDouble (name: string) (value: double) =
        if _mpv <> IntPtr.Zero then
            let mutable v = value
            use vPtr = fixed &v
            MpvNative.mpv_set_property(_mpv, name, MpvFormat.Double, NativePtr.toNativeInt vPtr) |> ignore

    let setFlag (name: string) (value: bool) =
        if _mpv <> IntPtr.Zero then
            let mutable v = if value then 1 else 0
            use vPtr = fixed &v
            MpvNative.mpv_set_property(_mpv, name, MpvFormat.Flag, NativePtr.toNativeInt vPtr) |> ignore

    let setPropertyString (name: string) (value: string) =
        if _mpv <> IntPtr.Zero then
            MpvNative.mpv_set_property_string(_mpv, name, value) |> ignore

    let getDouble (name: string) =
        if _mpv = IntPtr.Zero then Double.NaN
        else
            let mutable v = 0.0
            use vPtr = fixed &v
            if MpvNative.mpv_get_property(_mpv, name, MpvFormat.Double, NativePtr.toNativeInt vPtr) >= 0 then v
            else Double.NaN

    let getFlag (name: string) =
        if _mpv = IntPtr.Zero then false
        else
            let mutable v = 0
            use vPtr = fixed &v
            if MpvNative.mpv_get_property(_mpv, name, MpvFormat.Flag, NativePtr.toNativeInt vPtr) >= 0 && v <> 0 then true
            else false

    let getString (name: string) =
        if _mpv = IntPtr.Zero then None
        else
            let ptr = MpvNative.mpv_get_property_string(_mpv, name)
            if ptr = IntPtr.Zero then None
            else
                try Some(Marshal.PtrToStringUTF8(ptr))
                finally MpvNative.mpv_free(ptr)

    let getInt64 (name: string) (fallback: int64) =
        if _mpv = IntPtr.Zero then fallback
        else
            let mutable v = 0L
            use vPtr = fixed &v
            if MpvNative.mpv_get_property(_mpv, name, MpvFormat.Int64, NativePtr.toNativeInt vPtr) >= 0 then v
            else fallback

    let command (args: string[]) =
        if _mpv = IntPtr.Zero then -1
        else
            let ptrs : IntPtr[] = Array.zeroCreate (args.Length + 1)
            try
                for i in 0 .. args.Length - 1 do
                    ptrs.[i] <- stringToUtf8 args.[i]
                ptrs.[args.Length] <- IntPtr.Zero
                
                let handle = GCHandle.Alloc(ptrs, GCHandleType.Pinned)
                try
                    MpvNative.mpv_command(_mpv, handle.AddrOfPinnedObject())
                finally
                    handle.Free()
            finally
                for p in ptrs do if p <> IntPtr.Zero then Marshal.FreeHGlobal(p)

    let observe (name: string) (format: MpvFormat) =
        if _mpv <> IntPtr.Zero then
            MpvNative.mpv_observe_property(_mpv, 0uL, name, format) |> ignore

    let markTracksChanged() = Interlocked.Increment(&_trackRevision) |> ignore

    let snapshot() =
        lock _stateLock (fun () ->
            _state.IsPlaying <- not _pause && Option.isSome _state.FilePath
            _state.IsLooping <- _loop
            let copy = MediaState()
            copy.FilePath <- _state.FilePath
            copy.Position <- _state.Position
            copy.Duration <- _state.Duration
            copy.IsPlaying <- _state.IsPlaying
            copy.IsMuted <- _state.IsMuted
            copy.Volume <- _state.Volume
            copy.Speed <- _state.Speed
            copy.IsLooping <- _state.IsLooping
            copy)

    let raiseStateChanged() = stateChanged.Trigger(self, snapshot())

    let applyPropertyChange (data: IntPtr) =
        if data = IntPtr.Zero then false
        else
            let prop = Marshal.PtrToStructure<MpvEventProperty>(data)
            let name = Marshal.PtrToStringAnsi(prop.name)
            if String.IsNullOrWhiteSpace(name) || prop.data = IntPtr.Zero then false
            else
                let mutable shouldRaise = true
                lock _stateLock (fun () ->
                    match name with
                    | "time-pos" when prop.format = MpvFormat.Double ->
                        _state.Position <- TimeSpan.FromSeconds(Math.Max(0.0, Marshal.PtrToStructure<double>(prop.data)))
                        shouldRaise <- false
                    | "duration" when prop.format = MpvFormat.Double ->
                        _state.Duration <- TimeSpan.FromSeconds(Math.Max(0.0, Marshal.PtrToStructure<double>(prop.data)))
                        shouldRaise <- false
                    | "pause" when prop.format = MpvFormat.Flag ->
                        _pause <- Marshal.PtrToStructure<int>(prop.data) <> 0
                    | "mute" when prop.format = MpvFormat.Flag ->
                        _state.IsMuted <- Marshal.PtrToStructure<int>(prop.data) <> 0
                    | "volume" when prop.format = MpvFormat.Double ->
                        _state.Volume <- int(Math.Round(Math.Clamp(Marshal.PtrToStructure<double>(prop.data), 0.0, 150.0)))
                    | "speed" when prop.format = MpvFormat.Double ->
                        _state.Speed <- float32(Math.Clamp(Marshal.PtrToStructure<double>(prop.data), 0.25, 4.0))
                    | "aid" | "sid" ->
                        markTracksChanged()
                    | _ -> shouldRaise <- false
                )
                shouldRaise

    let eventLoop() =
        while not _disposed && _mpv <> IntPtr.Zero do
            let evtPtr = MpvNative.mpv_wait_event(_mpv, 0.25)
            let evt = Marshal.PtrToStructure<MpvEvent>(evtPtr)
            if evt.event_id <> MpvEventId.None then
                match evt.event_id with
                | MpvEventId.Shutdown -> _disposed <- true
                | MpvEventId.FileLoaded ->
                    lock _stateLock (fun () -> _pause <- getFlag "pause")
                    markTracksChanged()
                    raiseStateChanged()
                | MpvEventId.TracksChanged | MpvEventId.TrackSwitched ->
                    markTracksChanged()
                    raiseStateChanged()
                | MpvEventId.EndFile ->
                    if not _loop && evt.data <> IntPtr.Zero then
                        let endFileEvt = Marshal.PtrToStructure<MpvEventEndFile>(evt.data)
                        if endFileEvt.reason = 0 then
                            endReached.Trigger(self, EventArgs.Empty)
                    raiseStateChanged()
                | MpvEventId.Pause ->
                    lock _stateLock (fun () -> _pause <- true)
                    raiseStateChanged()
                | MpvEventId.Unpause ->
                    lock _stateLock (fun () -> _pause <- false)
                    raiseStateChanged()
                | MpvEventId.PropertyChange ->
                    if applyPropertyChange evt.data then
                        raiseStateChanged()
                | _ -> ()

    do
        try
            MpvNative.mpv_set_option_string(_mpv, "terminal", "no") |> ignore
            MpvNative.mpv_set_option_string(_mpv, "idle", "yes") |> ignore
            MpvNative.mpv_set_option_string(_mpv, "vo", "libmpv") |> ignore
            MpvNative.mpv_set_option_string(_mpv, "ao", "wasapi") |> ignore
            MpvNative.mpv_set_option_string(_mpv, "hwdec", "auto") |> ignore
            MpvNative.mpv_set_option_string(_mpv, "osd-level", "1") |> ignore
            MpvNative.mpv_initialize(_mpv) |> ignore
            
            observe "time-pos" MpvFormat.Double
            observe "duration" MpvFormat.Double
            observe "pause" MpvFormat.Flag
            observe "mute" MpvFormat.Flag
            observe "volume" MpvFormat.Double
            observe "speed" MpvFormat.Double
            observe "aid" MpvFormat.String
            observe "sid" MpvFormat.String
            
            self.SetVolume(_state.Volume)
            
            let t = Thread(ThreadStart(eventLoop))
            t.IsBackground <- true
            t.Name <- "ImPlay mpv events"
            t.Start()
            _eventThread <- Some t
        with
        | :? DllNotFoundException ->
            _initializationError <- Some "libmpv is not installed. Install mpv/libmpv and restart ImPlay."
        | ex ->
            _initializationError <- Some $"mpv could not be initialized: {ex.Message}"

    member _.InitializationError = _initializationError
    member _.MpvHandle = _mpv
    
    member _.CurrentFilePath = _state.FilePath

    [<CLIEvent>] member _.VideoFrameCaptured = videoFrameCaptured.Publish
    [<CLIEvent>] member _.StateChanged = stateChanged.Publish
    [<CLIEvent>] member _.EndReached = endReached.Publish
    [<CLIEvent>] member _.ErrorOccurred = errorOccurred.Publish

    member _.SetVolume(v: int) =
        let clamped = Math.Clamp(v, 0, 150)
        lock _stateLock (fun () -> _state.Volume <- clamped)
        setDouble "volume" (float v)
        raiseStateChanged()

    member _.SetSpeed(s: float32) =
        let clamped = Math.Clamp(s, 0.25f, 4.0f)
        lock _stateLock (fun () -> _state.Speed <- clamped)
        setDouble "speed" (float clamped)
        raiseStateChanged()

    member _.TogglePlayPause() =
        if _mpv <> IntPtr.Zero && Option.isSome _state.FilePath then
            setFlag "pause" (not _pause)

    member _.ToggleMute() =
        if _mpv <> IntPtr.Zero then
            setFlag "mute" (not (getFlag "mute"))

    member _.Seek(pos: TimeSpan) =
        if _mpv <> IntPtr.Zero then
            setDouble "time-pos" pos.TotalSeconds

    member _.OpenAsync(filePath: string, resumePosition: TimeSpan) =
        Task.Run(fun () ->
            if _mpv <> IntPtr.Zero then
                lock _stateLock (fun () ->
                    _state.FilePath <- Some filePath
                    _state.Position <- TimeSpan.Zero
                    _state.Duration <- TimeSpan.Zero
                    _state.IsPlaying <- true
                    _pause <- false
                )
                command [| "loadfile"; filePath; "replace" |] |> ignore
                if resumePosition > TimeSpan.Zero then
                    Thread.Sleep(250)
                    self.Seek(resumePosition)
                raiseStateChanged()
        )

    member _.Snapshot() = snapshot()

    member _.InitializeRendererOpenGl(getProcAddress: Func<string, IntPtr>, requestRender: Action) =
        if _rendererInitialized then true
        else
            _requestRender <- Some requestRender
            let callback = MpvRenderUpdateCallback(fun _ -> _requestRender |> Option.iter (fun r -> r.Invoke()))
            _renderCallback <- Some callback
            
            let getProc = MpvOpenGlGetProcAddress(fun _ name ->
                let funcName = Marshal.PtrToStringAnsi(name)
                if String.IsNullOrWhiteSpace(funcName) then IntPtr.Zero
                else getProcAddress.Invoke(funcName)
            )
            _getProcAddressDelegate <- Some getProc
            
            let getProcPtr = Marshal.GetFunctionPointerForDelegate(getProc)
            let mutable glParams = MpvOpenGlInitParams()
            glParams.get_proc_address <- getProcPtr
            glParams.get_proc_address_ctx <- IntPtr.Zero
            
            let glParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(glParams))
            Marshal.StructureToPtr(glParams, glParamsPtr, false)
            
            try
                let apiType = stringToUtf8 "opengl"
                let pType = MpvRenderParam(MpvRenderParamType.ApiType, apiType)
                let pInit = MpvRenderParam(MpvRenderParamType.OpenGlInitParams, glParamsPtr)
                
                let parameters = [| pType; pInit; MpvRenderParam(MpvRenderParamType.Invalid, IntPtr.Zero) |]
                let handle = GCHandle.Alloc(parameters, GCHandleType.Pinned)
                try
                    if MpvNative.mpv_render_context_create(&_renderContext, _mpv, handle.AddrOfPinnedObject()) < 0 then false
                    else
                        MpvNative.mpv_render_context_set_update_callback(_renderContext, callback, IntPtr.Zero)
                        _rendererInitialized <- true
                        true
                finally
                    handle.Free()
            finally
                Marshal.FreeHGlobal(glParamsPtr)

    member _.RenderVideoDirect(parameters: nativeptr<MpvRenderParam>) =
        if _rendererInitialized && _renderContext <> IntPtr.Zero then
            MpvNative.mpv_render_context_render(_renderContext, NativePtr.toNativeInt parameters) |> ignore

    member _.ShutdownRenderer() =
        if _rendererInitialized then
            if _renderContext <> IntPtr.Zero then
                MpvNative.mpv_render_context_free(_renderContext)
                _renderContext <- IntPtr.Zero
            _rendererInitialized <- false

    member _.StepFrame() = if _mpv <> IntPtr.Zero then command [| "frame-step" |] |> ignore
    member _.StepFrameBack() = if _mpv <> IntPtr.Zero then command [| "frame-back-step" |] |> ignore
    
    member _.IsLooping 
        with get() = _loop
        and set(v) =
            _loop <- v
            setPropertyString "loop-file" (if v then "inf" else "no")
            raiseStateChanged()

    interface IDisposable with
        member _.Dispose() =
            if not _disposed then
                _disposed <- true
                if _mpv <> IntPtr.Zero then
                    MpvNative.mpv_terminate_destroy(_mpv)
                    _mpv <- IntPtr.Zero
