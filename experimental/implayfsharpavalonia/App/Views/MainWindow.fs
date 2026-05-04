namespace ImPlay.App.Views

open System
open System.ComponentModel
open System.Globalization
open System.IO
open System.Runtime.InteropServices
open System.Threading.Tasks
open System.Linq
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Platform.Storage
open Avalonia.Threading
open Avalonia.VisualTree
open ImPlay.App.Controls
open ImPlay.App.ViewModels
open System.Windows.Input

module private MediaFiles =
    let extensions =
        set [ ".mp4"; ".mkv"; ".avi"; ".mov"; ".webm"; ".mp3"; ".flac"; ".ogg"; ".opus"; ".aac"; ".wav"; ".m4a"; ".m4b"; ".wma" ]

    let isMediaFile (path: string) =
        extensions.Contains(Path.GetExtension(path).ToLowerInvariant())

type MainWindow() as self =
    inherit Window()

    let mutable _lastControlsPulseMs = 0L
    let mutable _hideControlsTimer : DispatcherTimer option = None
    let mutable _positionTimer : DispatcherTimer option = None

    let getViewModel() = 
        match self.DataContext with
        | :? MainViewModel as vm -> Some vm
        | _ -> None

    let showControls() =
        match getViewModel() with
        | None -> ()
        | Some vm ->
            let now = Environment.TickCount64
            if vm.ControlsVisible && now - _lastControlsPulseMs < 250L then ()
            else
                _lastControlsPulseMs <- now
                if not vm.ControlsVisible then
                    vm.ControlsVisible <- true
                    self.Cursor <- Cursor.Default
                
                _hideControlsTimer |> Option.iter (fun t -> t.Stop(); t.Start())

    let hideControls() =
        _hideControlsTimer |> Option.iter (fun t -> t.Stop())
        match getViewModel() with
        | None -> ()
        | Some vm ->
            if vm.IsPlaying || self.WindowState = WindowState.FullScreen then
                if not (String.IsNullOrWhiteSpace(vm.CurrentFilePath)) && not vm.UseNativeVideoHost then
                    vm.ControlsVisible <- false
                    self.Cursor <- Cursor(StandardCursorType.None)

    let reattachVulkanIfActive() =
        match getViewModel() with
        | Some vm when vm.UseNativeVideoHost ->
            Dispatcher.UIThread.Post(
                (fun () ->
                    let host = self.FindControl<NativeMpvVideoHost>("NativeVideoSurface")
                    if not (isNull (box host)) then
                        host.ReattachNativeVideoWindow()),
                DispatcherPriority.Render)
        | _ -> ()

    let onDragOver (e: DragEventArgs) =
        e.DragEffects <- if e.DataTransfer.Contains(DataFormat.File) then DragDropEffects.Copy else DragDropEffects.None
        e.Handled <- true

    let onDrop (e: DragEventArgs) =
        let files = e.DataTransfer.TryGetFiles()
        e.Handled <- true
        match getViewModel() with
        | None -> ()
        | Some vm ->
            if not (isNull files) then
                let paths = 
                    files 
                    |> Seq.choose (fun f -> f.TryGetLocalPath() |> Option.ofObj)
                    |> Seq.toList
                match paths with
                | head :: _ ->
                    async {
                        do! vm.OpenFileAsync(head) |> Async.AwaitTask
                        reattachVulkanIfActive()
                    } |> Async.StartImmediate
                | _ -> ()

    let onWindowKeyDown (e: KeyEventArgs) =
        match getViewModel() with
        | None -> ()
        | Some vm ->
            let ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control)
            let alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt)
            match e.Key with
            | Key.Space -> 
                (vm.TogglePlayPauseCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.Left when ctrl ->
                (vm.SeekBackward30Command :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.Left ->
                (vm.SeekBackwardCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.Right when ctrl ->
                (vm.SeekForward30Command :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.Right ->
                (vm.SeekForwardCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.Up ->
                (vm.VolumeUpCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.Down ->
                (vm.VolumeDownCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.M ->
                (vm.ToggleMuteCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.OemOpenBrackets ->
                (vm.SpeedDownCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.OemCloseBrackets ->
                (vm.SpeedUpCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.OemBackslash ->
                (vm.ResetSpeedCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.OemPeriod ->
                (vm.StepFrameCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.OemComma ->
                (vm.StepFrameBackCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.PageUp ->
                (vm.PreviousChapterCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.PageDown ->
                (vm.NextChapterCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | Key.L ->
                (vm.ToggleLoopCommand :> ICommand).Execute(null)
                e.Handled <- true
            | Key.A ->
                (vm.CycleAudioTrackCommand :> ICommand).Execute(null)
                e.Handled <- true
            | Key.V ->
                (vm.CycleSubtitleTrackCommand :> ICommand).Execute(null)
                e.Handled <- true
            | Key.S when not ctrl ->
                self.LoadSubtitle_Click(null, null)
                e.Handled <- true
            | Key.Escape when self.WindowState = WindowState.FullScreen ->
                self.WindowState <- WindowState.Normal
                e.Handled <- true
            | Key.F ->
                self.WindowState <- if self.WindowState = WindowState.FullScreen then WindowState.Normal else WindowState.FullScreen
                e.Handled <- true
            | Key.T ->
                (vm.ToggleAlwaysOnTopCommand :> ICommand).Execute(null)
                e.Handled <- true
            | Key.O ->
                self.OpenButton_OnClick(null, null)
                e.Handled <- true
            | Key.G when ctrl ->
                self.JumpToTime_Click(null, null)
                e.Handled <- true
            | Key.F1 | Key.OemQuestion ->
                self.KeyboardShortcutsButton_OnClick(null, null)
                e.Handled <- true
            | Key.I when alt ->
                (vm.TakeScreenshotCommand :> ICommand).Execute(null)
                e.Handled <- true
            | Key.N ->
                (vm.NextTrackCommand :> ICommand).Execute(null)
                e.Handled <- true
            | Key.P ->
                (vm.PreviousTrackCommand :> ICommand).Execute(null)
                e.Handled <- true
            | Key.Q ->
                (vm.TogglePlaylistCommand :> ICommand).Execute(null)
                e.Handled <- true
            | Key.B ->
                self.BookmarksButton_Click(null, null)
                e.Handled <- true
            | Key.R when ctrl ->
                (vm.ToggleVideoRendererCommand :> ICommand).Execute(null)
                showControls()
                e.Handled <- true
            | _ -> ()

    let menuItems (cm: ContextMenu) =
        cm.Items |> Seq.cast<obj> |> Seq.choose (function | :? MenuItem as item -> Some item | _ -> None)

    let childMenuItems (item: MenuItem) =
        item.Items |> Seq.cast<obj> |> Seq.choose (function | :? MenuItem as child -> Some child | _ -> None)

    let findMenuItem name (cm: ContextMenu) =
        menuItems cm |> Seq.tryFind (fun item -> item.Name = name)

    let checkedHeader isSelected text =
        if isSelected then $"{text} (active)" else text

    let rendererLabel (tag: string) =
        if tag = "OpenGl" then "OpenGL (libmpv fallback)" else "Vulkan (native window)"

    let populateTrackMenu (menuItem: MenuItem) (tracks: TrackInfo[]) (execute: ICommand) =
        menuItem.Items.Clear()
        menuItem.IsEnabled <- tracks.Length > 0
        if tracks.Length = 0 then
            menuItem.Items.Add(MenuItem(Header = "None", IsEnabled = false)) |> ignore
        else
            for track in tracks do
                let mi = MenuItem(Header = checkedHeader track.IsSelected track.Name, Tag = track.Id)
                mi.Click.Add(fun _ -> execute.Execute(track.Id))
                menuItem.Items.Add(mi) |> ignore

    let openFolderAsync() =
        async {
            match getViewModel() with
            | None -> ()
            | Some vm ->
                let! folders =
                    self.StorageProvider.OpenFolderPickerAsync(
                        FolderPickerOpenOptions(Title = "Open folder", AllowMultiple = false))
                    |> Async.AwaitTask

                let dir =
                    folders
                    |> Seq.tryHead
                    |> Option.bind (fun folder -> folder.TryGetLocalPath() |> Option.ofObj)

                match dir with
                | Some folderPath when Directory.Exists(folderPath) ->
                    let mediaFiles =
                        Directory.EnumerateFiles(folderPath)
                        |> Seq.filter MediaFiles.isMediaFile
                        |> Seq.sortBy Path.GetFileName
                        |> Seq.toList
                    if mediaFiles.Length > 0 then
                        do! vm.LoadFilesAsync(mediaFiles) |> Async.AwaitTask
                        self.Focus() |> ignore
                        showControls()
                        reattachVulkanIfActive()
                | _ -> ()
        }

    let addToQueueAsync() =
        async {
            match getViewModel() with
            | None -> ()
            | Some vm ->
                let! files =
                    self.StorageProvider.OpenFilePickerAsync(
                        FilePickerOpenOptions(Title = "Add to queue", AllowMultiple = true))
                    |> Async.AwaitTask
                let paths =
                    files
                    |> Seq.choose (fun file -> file.TryGetLocalPath() |> Option.ofObj)
                    |> Seq.toList
                if paths.Length > 0 then
                    do! vm.AddFilesAsync(paths) |> Async.AwaitTask
                    reattachVulkanIfActive()
        }

    let openSubtitleFileAsync() =
        async {
            match getViewModel() with
            | None -> ()
            | Some vm ->
                let! files =
                    self.StorageProvider.OpenFilePickerAsync(
                        FilePickerOpenOptions(Title = "Load subtitle", AllowMultiple = false))
                    |> Async.AwaitTask
                let path =
                    files
                    |> Seq.tryHead
                    |> Option.bind (fun file -> file.TryGetLocalPath() |> Option.ofObj)
                match path with
                | Some subtitlePath ->
                    do! vm.LoadSubtitleFileAsync(subtitlePath) |> Async.AwaitTask
                    self.Focus() |> ignore
                | None -> ()
        }

    do
        self.InitializeComponent()
        
        _positionTimer <- Some(DispatcherTimer(TimeSpan.FromMilliseconds(200.0), DispatcherPriority.Normal, (fun _ _ -> ())))
        _positionTimer |> Option.iter (fun t -> t.Start())
        
        _hideControlsTimer <- Some(DispatcherTimer(TimeSpan.FromSeconds(3.0), DispatcherPriority.Normal, (fun _ _ -> hideControls())))
        
        self.AddHandler(DragDrop.DragOverEvent, EventHandler<DragEventArgs>(fun _ e -> onDragOver e))
        self.AddHandler(DragDrop.DropEvent, EventHandler<DragEventArgs>(fun _ e -> onDrop e))
        self.AddHandler(InputElement.KeyDownEvent, EventHandler<KeyEventArgs>(fun _ e -> onWindowKeyDown e), RoutingStrategies.Tunnel)
        
        self.PointerMoved.Add(fun _ -> showControls())
        
        self.Opened.Add(fun _ -> 
            self.Focus() |> ignore
        )
        
        self.Closed.Add(fun _ -> 
            match getViewModel() with
            | Some vm -> (vm :> IDisposable).Dispose()
            | _ -> ()
        )

    member private _.InitializeComponent() =
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(self)

    // ── XAML Event Handlers ───────────────────────────────────────────
    member _.OpenButton_OnClick(sender: obj, e: RoutedEventArgs) =
        async {
            let! files = self.StorageProvider.OpenFilePickerAsync(FilePickerOpenOptions(Title = "Open Media File", AllowMultiple = true)) |> Async.AwaitTask
            match getViewModel() with
            | Some vm ->
                let paths =
                    files
                    |> Seq.choose (fun file -> file.TryGetLocalPath() |> Option.ofObj)
                    |> Seq.toList
                match paths with
                | [ single ] -> do! vm.OpenFileAsync(single) |> Async.AwaitTask
                | _ when paths.Length > 1 -> do! vm.LoadFilesAsync(paths) |> Async.AwaitTask
                | _ -> ()
                self.Focus() |> ignore
                showControls()
                reattachVulkanIfActive()
            | _ -> ()
        } |> Async.StartImmediate

    member _.LoadSubtitleButton_OnClick(sender: obj, e: RoutedEventArgs) =
        let dialog = SubtitleSettingsDialog()
        dialog.ShowDialog(self) |> ignore

    member _.VideoAdjustmentsButton_OnClick(sender: obj, e: RoutedEventArgs) =
        let dialog = VideoAdjustmentsDialog()
        dialog.ShowDialog(self) |> ignore

    member _.AboutButton_Click(sender: obj, e: RoutedEventArgs) =
        let dialog = AboutDialog()
        dialog.ShowDialog(self) |> ignore

    member _.KeyboardShortcutsButton_OnClick(sender: obj, e: RoutedEventArgs) =
        let dialog = KeyboardShortcutsDialog()
        dialog.ShowDialog(self) |> ignore

    member _.MinimizeButton_Click(sender: obj, e: RoutedEventArgs) = self.WindowState <- WindowState.Minimized
    member _.MaximizeButton_Click(sender: obj, e: RoutedEventArgs) = self.WindowState <- if self.WindowState = WindowState.Maximized then WindowState.Normal else WindowState.Maximized
    member _.CloseButton_Click(sender: obj, e: RoutedEventArgs) = self.Close()
    member _.TitleBar_PointerPressed(sender: obj, e: PointerPressedEventArgs) = self.BeginMoveDrag(e)
    
    member _.VideoClickLayer_OnPointerPressed(sender: obj, e: PointerPressedEventArgs) =
        let prop = e.GetCurrentPoint(self).Properties
        if prop.IsLeftButtonPressed then
            if e.ClickCount = 2 then
                self.WindowState <- if self.WindowState = WindowState.FullScreen then WindowState.Normal else WindowState.FullScreen
            else
                match getViewModel() with
                | Some vm -> (vm.TogglePlayPauseCommand :> ICommand).Execute(null)
                | _ -> ()
        elif prop.IsRightButtonPressed then
            // Context menu is handled by XAML
            ()

    member _.RecentItem_Click(sender: obj, e: RoutedEventArgs) =
        match sender with
        | :? Button as btn ->
            let path = btn.Tag :?> string
            match getViewModel() with
            | Some vm ->
                async {
                    do! vm.OpenFileAsync(path) |> Async.AwaitTask
                    reattachVulkanIfActive()
                } |> Async.StartImmediate
            | _ -> ()
        | _ -> ()

    member _.CastButton_Click(sender: obj, e: RoutedEventArgs) =
        let dialog = CastDialog()
        dialog.ShowDialog(self) |> ignore

    member _.BookmarksButton_Click(sender: obj, e: RoutedEventArgs) =
        let dialog = BookmarksDialog()
        dialog.ShowDialog(self) |> ignore

    member _.FullscreenButton_OnClick(sender: obj, e: RoutedEventArgs) = self.WindowState <- if self.WindowState = WindowState.FullScreen then WindowState.Normal else WindowState.FullScreen
    member _.SeekSlider_OnSeekCommitted(sender: obj, e: RoutedEventArgs) = ()
    member _.RootContextMenu_Opening(sender: obj, e: CancelEventArgs) =
        match sender, getViewModel() with
        | :? ContextMenu as cm, Some vm ->
            match findMenuItem "RecentFilesMenuItem" cm with
            | Some recentMenuItem ->
                recentMenuItem.Items.Clear()
                recentMenuItem.IsEnabled <- vm.RecentFiles.Count > 0
                for path in vm.RecentFiles do
                    let mi = MenuItem(Header = Path.GetFileName(path))
                    ToolTip.SetTip(mi, path)
                    mi.Click.Add(fun _ ->
                        async {
                            do! vm.OpenFileAsync(path) |> Async.AwaitTask
                            self.Focus() |> ignore
                            showControls()
                            reattachVulkanIfActive()
                        } |> Async.StartImmediate)
                    recentMenuItem.Items.Add(mi) |> ignore
            | None -> ()

            match findMenuItem "AudioTrackMenuItem" cm with
            | Some audioMenuItem -> populateTrackMenu audioMenuItem vm.AudioTracks (vm.SetAudioTrackCommand :> ICommand)
            | None -> ()

            match findMenuItem "SubtitleTrackMenuItem" cm with
            | Some subtitleMenuItem -> populateTrackMenu subtitleMenuItem vm.SubtitleTracks (vm.SetSubtitleTrackCommand :> ICommand)
            | None -> ()

            match findMenuItem "AlwaysOnTopMenuItem" cm with
            | Some alwaysOnTopMenuItem ->
                alwaysOnTopMenuItem.Header <- if vm.IsAlwaysOnTop then "✓ Always on Top" else "Always on Top"
            | None -> ()

            match findMenuItem "VideoRendererMenuItem" cm with
            | Some rendererMenuItem ->
                rendererMenuItem.Header <- $"Video Renderer: {vm.VideoRendererLabel}"
                for item in childMenuItems rendererMenuItem do
                    let tag = if isNull item.Tag then "" else item.Tag.ToString()
                    let label = rendererLabel tag
                    item.Header <- checkedHeader (String.Equals(tag, vm.VideoRenderer.ToString(), StringComparison.OrdinalIgnoreCase)) label
            | None -> ()
        | _ -> ()

    member _.OpenFolder_Click(sender: obj, e: RoutedEventArgs) = openFolderAsync() |> Async.StartImmediate
    member _.AddToQueue_Click(sender: obj, e: RoutedEventArgs) = addToQueueAsync() |> Async.StartImmediate
    member _.JumpToTime_Click(sender: obj, e: RoutedEventArgs) =
        let dialog = JumpToTimeDialog()
        dialog.ShowDialog(self) |> ignore

    member _.LoadSubtitle_Click(sender: obj, e: RoutedEventArgs) = openSubtitleFileAsync() |> Async.StartImmediate
    member _.VideoAdjustments_Click(sender: obj, e: RoutedEventArgs) = self.VideoAdjustmentsButton_OnClick(sender, e)
    member _.Cast_Click(sender: obj, e: RoutedEventArgs) = self.CastButton_Click(sender, e)
    member _.KeyboardShortcuts_Click(sender: obj, e: RoutedEventArgs) = self.KeyboardShortcutsButton_OnClick(sender, e)
    member _.Bookmarks_Click(sender: obj, e: RoutedEventArgs) = self.BookmarksButton_Click(sender, e)
    member _.VideoRenderer_Click(sender: obj, e: RoutedEventArgs) =
        match sender, getViewModel() with
        | :? MenuItem as item, Some vm when not (isNull item.Tag) ->
            (vm.SetVideoRendererCommand :> ICommand).Execute(item.Tag)
            if String.Equals(item.Tag.ToString(), "NativeVulkan", StringComparison.OrdinalIgnoreCase) then
                let host = self.FindControl<NativeMpvVideoHost>("NativeVideoSurface")
                if not (isNull (box host)) then
                    host.ReattachNativeVideoWindow()
            showControls()
        | _ -> ()
    member _.Speed_Click(sender: obj, e: RoutedEventArgs) =
        match sender, getViewModel() with
        | :? MenuItem as item, Some vm when not (isNull item.Tag) ->
            (vm.SetSpeedCommand :> ICommand).Execute(item.Tag)
        | _ -> ()
    member _.AlwaysOnTop_Click(sender: obj, e: RoutedEventArgs) =
        match getViewModel() with
        | Some vm ->
            (vm.ToggleAlwaysOnTopCommand :> ICommand).Execute(null)
            self.Topmost <- vm.IsAlwaysOnTop
        | _ -> ()
    member _.About_Click(sender: obj, e: RoutedEventArgs) = self.AboutButton_Click(sender, e)
    member _.PlaylistDragHandle_PointerPressed(sender: obj, e: PointerPressedEventArgs) = ()
    member _.PlaylistCtx_Play(sender: obj, e: RoutedEventArgs) = ()
    member _.PlaylistCtx_Remove(sender: obj, e: RoutedEventArgs) = ()
    member _.PlaylistCtx_AddFile(sender: obj, e: RoutedEventArgs) = ()
    member _.PlaylistCtx_AddFolder(sender: obj, e: RoutedEventArgs) = ()
