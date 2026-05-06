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

[<AllowNullLiteral>]
type PlaybackService() as self =
    let mutable _mpv = IntPtr.Zero
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
    let mutable _lastRenderFramebuffer = -1
    let mutable _lastRenderWidth = 0
    let mutable _lastRenderHeight = 0
    let mutable _nativeVideoWindow = IntPtr.Zero
    
    let mutable _eventThread : Thread option = None
    
    let mutable _initializationError : string option = None
    
    let languageNames = 
        Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        |> fun d ->
            d.["ara"] <- "Arabic"
            d.["chi"] <- "Chinese"
            d.["zho"] <- "Chinese"
            d.["dan"] <- "Danish"
            d.["dut"] <- "Dutch"
            d.["nld"] <- "Dutch"
            d.["eng"] <- "English"
            d.["fin"] <- "Finnish"
            d.["fre"] <- "French"
            d.["fra"] <- "French"
            d.["ger"] <- "German"
            d.["deu"] <- "German"
            d.["hin"] <- "Hindi"
            d.["ita"] <- "Italian"
            d.["jpn"] <- "Japanese"
            d.["kor"] <- "Korean"
            d.["nor"] <- "Norwegian"
            d.["pol"] <- "Polish"
            d.["por"] <- "Portuguese"
            d.["rus"] <- "Russian"
            d.["spa"] <- "Spanish"
            d.["swe"] <- "Swedish"
            d.["tur"] <- "Turkish"
            d

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

    let formatLanguageName (lang: string) =
        let code = lang.Trim()
        match languageNames.TryGetValue(code) with
        | true, name -> name
        | _ ->
            try
                if code.Length = 2 then Globalization.CultureInfo.GetCultureInfo(code).EnglishName
                else code
            with _ -> code

    let buildTrackName prefix trackType id =
        let title = getString $"{prefix}/title"
        let lang = getString $"{prefix}/lang"
        let codec = getString $"{prefix}/codec"
        let external = getFlag $"{prefix}/external"
        
        let fallback = if trackType = "audio" then $"Audio {id}" else $"Subtitle {id}"
        let main = title |> Option.defaultValue fallback
        
        let details = List<string>()
        lang |> Option.iter (fun l -> details.Add(formatLanguageName l))
        codec |> Option.iter (fun c -> details.Add(c))
        if external then details.Add("external")
        
        if details.Count = 0 then main
        else $"""{main} ({String.Join(", ", details)})"""

    let getSelectedTrackId trackType =
        let count = getInt64 "track-list/count" 0L
        let mutable result = -1
        let mutable i = 0
        while i < int count && result = -1 do
            let prefix = $"track-list/{i}"
            if getString $"{prefix}/type" = Some trackType && getFlag $"{prefix}/selected" then
                result <- int(getInt64 $"{prefix}/id" -1L)
            i <- i + 1
        result

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
                | MpvEventId.LogMessage ->
                    if evt.data <> IntPtr.Zero then
                        let msg = Marshal.PtrToStructure<MpvEventLogMessage>(evt.data)
                        let text = Marshal.PtrToStringAnsi(msg.text)
                        let level = Marshal.PtrToStringAnsi(msg.level)
                        StartupLogger.log $"[mpv] {level}: {text}"
                | _ -> ()

    do
        try
            _mpv <- MpvNative.mpv_create()
            if _mpv <> IntPtr.Zero then
                MpvNative.mpv_set_option_string(_mpv, "config", "no") |> ignore
                MpvNative.mpv_set_option_string(_mpv, "terminal", "no") |> ignore
                MpvNative.mpv_set_option_string(_mpv, "idle", "yes") |> ignore
                MpvNative.mpv_set_option_string(_mpv, "vo", "libmpv") |> ignore
                MpvNative.mpv_set_option_string(_mpv, "ao", "wasapi") |> ignore
                MpvNative.mpv_set_option_string(_mpv, "hwdec", "auto") |> ignore
                MpvNative.mpv_set_option_string(_mpv, "osd-level", "1") |> ignore
                MpvNative.mpv_request_log_messages(_mpv, "info") |> ignore
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
    member _.Context = _mpv
    member _.TrackRevision = Interlocked.Read(&_trackRevision)
    
    member _.CurrentFilePath = _state.FilePath
    member _.Position = _state.Position
    member _.Duration = _state.Duration
    member _.IsPlaying = _state.IsPlaying
    member _.IsMuted = _state.IsMuted
    member _.Volume = _state.Volume
    member _.Speed = _state.Speed
    member _.IsLooping = _state.IsLooping

    [<CLIEvent>] member _.VideoFrameCaptured = videoFrameCaptured.Publish
    [<CLIEvent>] member _.StateChanged = stateChanged.Publish
    [<CLIEvent>] member _.EndReached = endReached.Publish
    [<CLIEvent>] member _.ErrorOccurred = errorOccurred.Publish

    member _.SetVolume(v: int) =
        let clamped = Math.Clamp(v, 0, 150)
        lock _stateLock (fun () -> _state.Volume <- clamped)
        setDouble "volume" (float clamped)
        raiseStateChanged()

    member self.ChangeVolume(delta: int) = self.SetVolume(self.Volume + delta)

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
        if _mpv <> IntPtr.Zero && Option.isSome _state.FilePath then
            let clamped = if pos < TimeSpan.Zero then TimeSpan.Zero else pos
            setDouble "time-pos" clamped.TotalSeconds
            lock _stateLock (fun () -> _state.Position <- clamped)
            raiseStateChanged()

    member _.OpenAsync(filePath: string, resumePosition: TimeSpan) =
        Task.Run(fun () ->
            if _mpv <> IntPtr.Zero then
                if not (File.Exists(filePath)) then
                    errorOccurred.Trigger(self, "The selected file does not exist.")
                else
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

    member _.Load(path: string) =
        let isSubtitleFile (p: string) =
            let ext = Path.GetExtension(p).ToLowerInvariant()
            match ext with
            | ".srt" | ".vtt" | ".ass" | ".ssa" | ".sub" | ".idx" -> true
            | _ -> false

        if isSubtitleFile path then self.LoadSubtitleFile(path)
        else self.OpenAsync(path, TimeSpan.Zero) |> ignore

    member _.Snapshot() = snapshot()

    member self.InitializeRenderer(getProcAddress, requestRender) = self.InitializeRendererOpenGl(getProcAddress, requestRender)
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

    member self.RenderVideo(fbo: int, width: int, height: int) =
        if _rendererInitialized && _renderContext <> IntPtr.Zero && width > 0 && height > 0 then
            let updateFlags = MpvNative.mpv_render_context_update(_renderContext)
            let targetChanged = fbo <> _lastRenderFramebuffer || width <> _lastRenderWidth || height <> _lastRenderHeight
            
            // 1UL << 0 is MpvRenderUpdateFrame
            if (updateFlags &&& 1uL) <> 0uL || targetChanged then
                let mutable glFbo = MpvOpenGlFbo()
                glFbo.fbo <- fbo
                glFbo.w <- width
                glFbo.h <- height
                glFbo.internal_format <- 0
                
                let glFboPtr = Marshal.AllocHGlobal(Marshal.SizeOf(glFbo))
                Marshal.StructureToPtr(glFbo, glFboPtr, false)
                
                let flipPtr = Marshal.AllocHGlobal(4)
                Marshal.WriteInt32(flipPtr, 1)
                
                try
                    let mutable pFbo = MpvRenderParam(MpvRenderParamType.OpenGlFbo, glFboPtr)
                    let mutable pFlip = MpvRenderParam(MpvRenderParamType.FlipY, flipPtr)
                    let mutable parameters = [| pFbo; pFlip; MpvRenderParam(MpvRenderParamType.Invalid, IntPtr.Zero) |]
                    
                    let handle = GCHandle.Alloc(parameters, GCHandleType.Pinned)
                    try
                        self.RenderVideoDirect(NativePtr.ofNativeInt (handle.AddrOfPinnedObject()))
                    finally
                        handle.Free()
                finally
                    Marshal.FreeHGlobal(glFboPtr)
                    Marshal.FreeHGlobal(flipPtr)
                
                _lastRenderFramebuffer <- fbo
                _lastRenderWidth <- width
                _lastRenderHeight <- height

    member _.RenderVideoDirect(parameters: nativeptr<MpvRenderParam>) =
        if _rendererInitialized && _renderContext <> IntPtr.Zero then
            MpvNative.mpv_render_context_render(_renderContext, NativePtr.toNativeInt parameters) |> ignore

    member _.ShutdownRenderer() =
        if _rendererInitialized then
            if _renderContext <> IntPtr.Zero then
                MpvNative.mpv_render_context_free(_renderContext)
                _renderContext <- IntPtr.Zero
            _rendererInitialized <- false

    member _.GetMetadata(key: string) = getString $"metadata/by-key/{key}"
    

    member _.SetBrightness(v: int)  = if _mpv <> IntPtr.Zero then command [| "set"; "brightness"; string(Math.Clamp(v, -100, 100)) |] |> ignore
    member _.SetContrast(v: int)    = if _mpv <> IntPtr.Zero then command [| "set"; "contrast"; string(Math.Clamp(v, -100, 100)) |] |> ignore
    member _.SetSaturation(v: int)  = if _mpv <> IntPtr.Zero then command [| "set"; "saturation"; string(Math.Clamp(v, -100, 100)) |] |> ignore
    member _.SetVideoRotation(deg: int) = if _mpv <> IntPtr.Zero then command [| "set"; "video-rotate"; string(((deg % 360) + 360) % 360) |] |> ignore
    member _.SetVideoZoom(zoom: double) = if _mpv <> IntPtr.Zero then command [| "set"; "video-zoom"; zoom.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) |] |> ignore
    member _.SetVideoAspect(aspect: string) = if _mpv <> IntPtr.Zero then command [| "set"; "video-aspect-override"; aspect |] |> ignore

    member _.TakeSnapshot(outputPath: string) =
        if _mpv <> IntPtr.Zero && Option.isSome _state.FilePath then
            command [| "screenshot-to-file"; outputPath; "video" |] >= 0
        else false

    member _.GetAudioTracks() = 
        let count = getInt64 "track-list/count" 0L
        let tracks = List<MediaTrack>()
        for i in 0 .. int(count) - 1 do
            let prefix = $"track-list/{i}"
            if getString $"{prefix}/type" = Some "audio" then
                let id = int(getInt64 $"{prefix}/id" -1L)
                let name = buildTrackName prefix "audio" id
                let selected = getFlag $"{prefix}/selected"
                tracks.Add({ Id = id; Name = name; IsSelected = selected })
        tracks.ToArray()

    member _.GetSubtitleTracks() =
        let count = getInt64 "track-list/count" 0L
        let tracks = List<MediaTrack>()
        tracks.Add({ Id = -1; Name = "Off"; IsSelected = (getSelectedTrackId "sub" < 0) })
        for i in 0 .. int(count) - 1 do
            let prefix = $"track-list/{i}"
            if getString $"{prefix}/type" = Some "sub" then
                let id = int(getInt64 $"{prefix}/id" -1L)
                let name = buildTrackName prefix "sub" id
                let selected = getFlag $"{prefix}/selected"
                tracks.Add({ Id = id; Name = name; IsSelected = selected })
        tracks.ToArray()

    member _.SetAudioTrack(id: int) = 
        setPropertyString "aid" (if id < 0 then "no" else string id)
        markTracksChanged()

    member _.SetVideoTrack(id: int) = 
        setPropertyString "vid" (if id < 0 then "no" else string id)
        markTracksChanged()

    member _.SetSubtitleTrack(id: int) = 
        setPropertyString "sid" (if id < 0 then "no" else string id)
        markTracksChanged()

    member _.SubtitleDelayMs
        with get() : int64 = int64(getDouble "sub-delay" * 1000.0)
        and set(v: int64) = setDouble "sub-delay" (float v / 1000.0)

    member self.GetVideoTracks() =
        let count = getInt64 "track-list/count" 0L
        let tracks = List<MediaTrack>()
        for i in 0 .. int(count) - 1 do
            let prefix = $"track-list/{i}"
            if getString $"{prefix}/type" = Some "video" then
                let id = int(getInt64 $"{prefix}/id" -1L)
                let name = buildTrackName prefix "video" id
                let selected = getFlag $"{prefix}/selected"
                tracks.Add({ Id = id; Name = name; IsSelected = selected })
        tracks.ToArray()

    member self.HasVideoTrack = self.GetVideoTracks().Length > 0

    member self.CycleAudioTrack() =
        let tracks = self.GetAudioTracks()
        if tracks.Length > 0 then
            let current = getSelectedTrackId "audio"
            let idx = Array.tryFindIndex (fun (t: MediaTrack) -> t.Id = current) tracks |> Option.defaultValue -1
            let next = tracks.[(idx + 1) % tracks.Length]
            self.SetAudioTrack(next.Id)

    member self.CycleSubtitleTrack() =
        let tracks = self.GetSubtitleTracks()
        if tracks.Length > 0 then
            let current = getSelectedTrackId "sub"
            let idx = Array.tryFindIndex (fun (t: MediaTrack) -> t.Id = current) tracks |> Option.defaultValue -1
            let next = tracks.[(idx + 1) % tracks.Length]
            self.SetSubtitleTrack(next.Id)

    member _.GetChapterPositions() =
        let count = getInt64 "chapter-list/count" 0L
        let positions = List<double>()
        for i in 0 .. int(count) - 1 do
            let t = getDouble $"chapter-list/{i}/time"
            if not (Double.IsNaN t) then positions.Add(t)
        positions.ToArray()

    member _.SeekToChapter(direction: int) =
        if _mpv <> IntPtr.Zero then
            command [| "add"; "chapter"; if direction > 0 then "1" else "-1" |] |> ignore

    member _.ToggleLoop() =
        _loop <- not _loop
        setPropertyString "loop-file" (if _loop then "inf" else "no")
        lock _stateLock (fun () -> _state.IsLooping <- _loop)
        raiseStateChanged()

    member _.CycleFileLoop() = if _mpv <> IntPtr.Zero then command [| "cycle-values"; "loop-file"; "inf"; "no" |] |> ignore
    member _.CyclePlaylistLoop() = if _mpv <> IntPtr.Zero then command [| "cycle-values"; "loop-playlist"; "inf"; "no" |] |> ignore
    member _.AbLoop() = if _mpv <> IntPtr.Zero then command [| "ab-loop" |] |> ignore

    member _.AddAudioDelay(seconds: double) = if _mpv <> IntPtr.Zero then command [| "add"; "audio-delay"; seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) |] |> ignore
    member _.AddSubtitleDelay(seconds: double) = if _mpv <> IntPtr.Zero then command [| "add"; "sub-delay"; seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) |] |> ignore
    member _.AddSubtitleScale(value: double) = if _mpv <> IntPtr.Zero then command [| "add"; "sub-scale"; value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) |] |> ignore
    member _.AddSubtitlePos(value: int) = if _mpv <> IntPtr.Zero then command [| "add"; "sub-pos"; string value |] |> ignore
    member _.ToggleSubtitleVisibility() = if _mpv <> IntPtr.Zero then command [| "cycle"; "sub-visibility" |] |> ignore

    member _.Pause() = if _mpv <> IntPtr.Zero && Option.isSome _state.FilePath then setFlag "pause" true
    member _.Play() = if _mpv <> IntPtr.Zero && Option.isSome _state.FilePath then setFlag "pause" false
    member _.Stop() =
        if _mpv <> IntPtr.Zero then
            command [| "stop" |] |> ignore
            lock _stateLock (fun () ->
                _state.FilePath <- None
                _state.Position <- TimeSpan.Zero
                _state.Duration <- TimeSpan.Zero
                _state.IsPlaying <- false
                _pause <- true
            )
            raiseStateChanged()
    
    member _.StepFrame() = if _mpv <> IntPtr.Zero then command [| "frame-step" |] |> ignore
    member _.StepFrameBack() = if _mpv <> IntPtr.Zero then command [| "frame-back-step" |] |> ignore
    
    member _.SeekRelative(seconds: TimeSpan) =
        if _mpv <> IntPtr.Zero && Option.isSome _state.FilePath then
            command [| "seek"; seconds.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); "relative"; "exact" |] |> ignore

    member _.LoadSubtitleFile(path: string) =
        if _mpv <> IntPtr.Zero && Option.isSome _state.FilePath then
            command [| "sub-add"; path; "cached" |] |> ignore
            markTracksChanged()

    member _.SetWindowHandle(handle: IntPtr) =
        if handle <> IntPtr.Zero then
            setPropertyString "wid" (handle.ToInt64().ToString())

    member self.UseNativeVideoWindow(handle: IntPtr, preferVulkan: bool) =
        if _mpv <> IntPtr.Zero && handle <> IntPtr.Zero then
            self.ShutdownRenderer()
            _nativeVideoWindow <- handle
            
            let vo = if preferVulkan then "gpu-next" else "gpu"
            MpvNative.mpv_set_option_string(_mpv, "vo", vo) |> ignore
            if preferVulkan then MpvNative.mpv_set_option_string(_mpv, "gpu-api", "vulkan") |> ignore
            MpvNative.mpv_set_option_string(_mpv, "hwdec", "auto-safe") |> ignore
            MpvNative.mpv_set_option_string(_mpv, "wid", handle.ToInt64().ToString(Globalization.CultureInfo.InvariantCulture)) |> ignore
            
            let api = if preferVulkan then "vulkan" else "auto"
            StartupLogger.log $"mpv native video window attached: hwnd=0x{handle.ToInt64():X}, vo={vo}, gpu-api={api}."
            true
        else false

    member _.DetachNativeVideoWindow(handle: IntPtr) =
        if _nativeVideoWindow = handle && _mpv <> IntPtr.Zero then
            _nativeVideoWindow <- IntPtr.Zero
            MpvNative.mpv_set_option_string(_mpv, "wid", "0") |> ignore
            StartupLogger.log $"mpv native video window detached: hwnd=0x{handle.ToInt64():X}."

    member _.SetPropertyString(name: string, value: string) = setPropertyString name value

    interface IDisposable with
        member _.Dispose() =
            if not _disposed then
                _disposed <- true
                if _mpv <> IntPtr.Zero then
                    MpvNative.mpv_terminate_destroy(_mpv)
                    _mpv <- IntPtr.Zero
