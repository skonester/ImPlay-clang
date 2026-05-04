namespace ImPlay.App.Controls

open System
open Avalonia
open Avalonia.Controls
open Avalonia.OpenGL
open Avalonia.OpenGL.Controls
open Avalonia.Threading
open System.Threading
open ImPlay.Core.Services

type MpvVideoSurface() as self =
    inherit OpenGlControlBase()

    let mutable _rendererInitialized = false
    let mutable _glReady = false
    let mutable _renderRequestQueued = 0
    let mutable _isReadyForPlaybackOpen = false
    let readyForPlaybackOpen = Event<EventHandler, EventArgs>()

    static let PlaybackProperty =
        AvaloniaProperty.Register<MpvVideoSurface, PlaybackService>("Playback")

    [<CLIEvent>]
    member _.ReadyForPlaybackOpen = readyForPlaybackOpen.Publish

    member _.IsReadyForPlaybackOpen
        with get() = _isReadyForPlaybackOpen
        and private set(value) =
            if _isReadyForPlaybackOpen <> value then
                _isReadyForPlaybackOpen <- value
                if value then
                    readyForPlaybackOpen.Trigger(self, EventArgs.Empty)

    member _.Playback
        with get() = self.GetValue(PlaybackProperty)
        and set(v) =
            self.SetValue(PlaybackProperty, v) |> ignore

    member private _.QueueRenderRequest() =
        if _glReady then
            if Interlocked.Exchange(&_renderRequestQueued, 1) <> 1 then
                Dispatcher.UIThread.Post(
                    (fun () ->
                        if _glReady then
                            self.RequestNextFrameRendering()
                        else
                            Interlocked.Exchange(&_renderRequestQueued, 0) |> ignore),
                    DispatcherPriority.Background)

    member private _.TryInitializeRenderer(gl: GlInterface) =
        if not _rendererInitialized && _glReady && not (isNull self.Playback) then
            _rendererInitialized <-
                self.Playback.InitializeRenderer(
                    Func<string, IntPtr>(fun proc -> gl.GetProcAddress(proc)),
                    Action(self.QueueRenderRequest))

    override _.OnPropertyChanged(change: AvaloniaPropertyChangedEventArgs) =
        base.OnPropertyChanged(change)

        if change.Property = PlaybackProperty && _glReady && not _rendererInitialized then
            self.RequestNextFrameRendering()

    override _.OnOpenGlInit(gl) =
        base.OnOpenGlInit(gl)
        StartupLogger.Log("OpenGL video surface initialized.")
        _glReady <- true
        self.TryInitializeRenderer(gl)
        self.IsReadyForPlaybackOpen <- _rendererInitialized
        if _rendererInitialized then
            self.RequestNextFrameRendering()

    override _.OnOpenGlDeinit(gl) =
        StartupLogger.Log("OpenGL video surface deinitialized.")
        self.IsReadyForPlaybackOpen <- false
        _glReady <- false
        _rendererInitialized <- false
        Interlocked.Exchange(&_renderRequestQueued, 0) |> ignore
        if not (isNull self.Playback) then
            self.Playback.ShutdownRenderer()
        base.OnOpenGlDeinit(gl)

    override _.OnOpenGlRender(gl, fb) =
        Interlocked.Exchange(&_renderRequestQueued, 0) |> ignore

        if _glReady && not (isNull self.Playback) then
            self.TryInitializeRenderer(gl)
            if _rendererInitialized then
                self.IsReadyForPlaybackOpen <- true

            let scale =
                match TopLevel.GetTopLevel(self) with
                | null -> 1.0
                | topLevel -> topLevel.RenderScaling
            let width = Math.Max(1, int (Math.Round(self.Bounds.Width * scale)))
            let height = Math.Max(1, int (Math.Round(self.Bounds.Height * scale)))
            self.Playback.RenderVideo(fb, width, height)
