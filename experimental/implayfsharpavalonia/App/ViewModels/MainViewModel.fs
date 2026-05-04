namespace ImPlay.App.ViewModels

open System
open System.Collections.ObjectModel
open System.Collections.Generic
open System.ComponentModel
open System.Globalization
open System.IO
open System.Windows.Input
open System.Threading.Tasks
open Avalonia.Media
open Avalonia.Threading
open ImPlay.Core.Models
open ImPlay.Core.Services

type RelayCommand(execute: obj -> unit) =
    let event = Event<EventHandler, EventArgs>()
    interface ICommand with
        [<CLIEvent>] member _.CanExecuteChanged = event.Publish
        member _.CanExecute(_) = true
        member _.Execute(parameter) = execute parameter
    member _.RaiseCanExecuteChanged() = event.Trigger(null, EventArgs.Empty)

type TrackInfo(id: int, name: string, isSelected: bool) =
    member _.Id = id
    member _.Name = name
    member _.IsSelected = isSelected

type PlaylistItem(index: int, filePath: string, isCurrent: bool) =
    member _.Index = index
    member _.FilePath = filePath
    member _.IsCurrent = isCurrent
    member _.DisplayName = Path.GetFileName(filePath)

type RecentFileItem(filePath: string, progressPct: double, resumePosition: TimeSpan) =
    member _.FilePath = filePath
    member _.ProgressPct = progressPct
    member _.ResumePosition = resumePosition
    member _.DisplayName = Path.GetFileNameWithoutExtension(filePath)
    member _.Directory = Path.GetDirectoryName(filePath) |> Option.ofObj |> Option.defaultValue ""
    member _.HasResume = progressPct >= 0.0
    member _.ResumeLabel =
        if resumePosition.TotalHours >= 1.0 then
            $"Resume from {int resumePosition.TotalHours}:{resumePosition.Minutes:D2}:{resumePosition.Seconds:D2}"
        else
            $"Resume from {resumePosition.Minutes}:{resumePosition.Seconds:D2}"

module private MediaKind =
    let audioExtensions =
        set [ ".mp3"; ".flac"; ".wav"; ".ogg"; ".m4a"; ".aac"; ".opus"; ".m4b"; ".wma" ]

    let isAudioFile (filePath: string) =
        audioExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant())

type MainViewModel(playback: PlaybackService, settings: SettingsService) as self =
    let mutable _playback = playback
    let mutable _settings = settings
    let mutable _title = "ImPlay"
    let mutable _timeText = "00:00 / 00:00"
    let mutable _errorMessage : string option = None
    let mutable _seekValue = 0.0
    let mutable _volume = 80
    let mutable _isPlaying = false
    let mutable _isMuted = false
    let mutable _controlsVisible = true
    let mutable _isSeeking = false
    let mutable _speed = 1.0f
    let mutable _isLooping = false
    let mutable _videoRenderer = VideoRendererKind.NativeVulkan
    
    let mutable _isAlwaysOnTop = false
    let mutable _isPlaylistVisible = false
    let mutable _hasVideoAdjustments = true
    let mutable _currentSubtitleText = ""
    let mutable _subtitleFontSize = 24.0
    let mutable _isAudioMode = false
    let mutable _hasMedia = false
    let mutable _osdMessage = ""
    let mutable _isCasting = false
    let mutable _castStatusText = ""
    let mutable _isCastPlaying = false
    let mutable _controlsOpacity = 1.0
    let mutable _seekStep = settings.SeekStep
    let mutable _seekStepLabel = $"{_seekStep}s"
    
    let _playlistItems = ObservableCollection<PlaylistItem>()
    let _playlistPaths = List<string>()
    let _recentFileItems = ObservableCollection<RecentFileItem>()
    let _chapterPositions = List<double>()
    let mutable _playlistIndex = -1
    
    let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
    
    let notify (name: string) =
        propertyChanged.Trigger(self, PropertyChangedEventArgs(name))

    let rebuildPlaylistItems() =
        _playlistItems.Clear()
        for i = 0 to _playlistPaths.Count - 1 do
            _playlistItems.Add(PlaylistItem(i, _playlistPaths.[i], (i = _playlistIndex)))
        notify "PlaylistItems"
        notify "PlaylistCount"
        notify "HasPlaylist"
        notify "PlaylistPositionLabel"

    let openAtIndex(index: int) =
        task {
            if index >= 0 && index < _playlistPaths.Count then
                _playlistIndex <- index
                rebuildPlaylistItems()
                let filePath = _playlistPaths.[index]
                _isAudioMode <- MediaKind.isAudioFile filePath
                _hasMedia <- true
                _title <- $"{Path.GetFileName(filePath)} - ImPlay"
                notify "IsAudioMode"
                notify "IsAudioOnly"
                notify "HasMedia"
                notify "UseNativeVideoHost"
                notify "UseOpenGlVideoSurface"
                notify "Title"
                if not _isAudioMode && _videoRenderer = VideoRendererKind.NativeVulkan && OperatingSystem.IsWindows() then
                    do! Task.Delay(75)
                let resumePos = _settings.GetResumePosition(filePath)
                do! _playback.OpenAsync(filePath, resumePos)
                _settings.AddRecentFile(filePath)
        }

    let refreshState() =
        let snap = _playback.Snapshot()
        _isPlaying <- snap.IsPlaying
        _isMuted <- snap.IsMuted
        _volume <- snap.Volume
        _speed <- snap.Speed
        _isLooping <- snap.IsLooping
        _hasMedia <- not (String.IsNullOrWhiteSpace(snap.FilePath))
        if _hasMedia then
            _isAudioMode <- MediaKind.isAudioFile snap.FilePath
        
        let pos = snap.Position
        let dur = snap.Duration
        _timeText <- sprintf "%02d:%02d / %02d:%02d" pos.Minutes pos.Seconds dur.Minutes dur.Seconds
        if not _isSeeking && dur.TotalSeconds > 0.0 then
            _seekValue <- (pos.TotalSeconds / dur.TotalSeconds) * 1000.0
            
        _title <- if not (String.IsNullOrWhiteSpace(snap.FilePath)) then $"{Path.GetFileName(snap.FilePath)} - ImPlay" else "ImPlay"
            
        notify "IsPlaying"
        notify "IsMuted"
        notify "Volume"
        notify "Speed"
        notify "IsLooping"
        notify "TimeText"
        notify "SeekValue"
        notify "Title"
        notify "HasMedia"
        notify "IsAudioMode"
        notify "IsAudioOnly"
        notify "UseNativeVideoHost"
        notify "UseOpenGlVideoSurface"
        notify "IsNormalSpeed"
        notify "SpeedLabel"

    do
        _volume <- _settings.LastVolume
        _speed <- _settings.LastSpeed
        _videoRenderer <- _settings.VideoRenderer
        _playback.SetVolume(_volume)
        if Math.Abs(float _speed - 1.0) > 0.001 then
            _playback.SetSpeed(_speed)
            
        _playback.StateChanged.Add(fun _ -> 
            Dispatcher.UIThread.Post(refreshState)
        )
        
        _playback.EndReached.Add(fun _ ->
            let currentPath = _playback.CurrentFilePath
            if not (String.IsNullOrWhiteSpace(currentPath)) then
                _settings.ClearResumePosition(currentPath)
            refreshState()
        )

    interface INotifyPropertyChanged with
        [<CLIEvent>] member _.PropertyChanged = propertyChanged.Publish

    member _.Playback = _playback
    member _.CurrentFilePath = _playback.CurrentFilePath
    
    member _.Title = _title
    member _.TimeText = _timeText
    member _.Volume
        with get() = _volume
        and set(v) = 
            let clamped = Math.Clamp(v, 0, 150)
            if _volume <> clamped then
                _volume <- clamped
                _playback.SetVolume(_volume)
                _settings.SaveSessionPreferences(_volume, _speed, _settings.SeekStep) |> ignore
                notify "Volume"

    member _.IsPlaying = _isPlaying
    member _.IsMuted = _isMuted
    member _.IsLooping = _isLooping
    member _.Speed = _speed
    member _.IsNormalSpeed = Math.Abs(float _speed - 1.0) < 0.01
    member _.SpeedLabel = sprintf "%.2fx" _speed
    
    member _.SeekValue
        with get() = _seekValue
        and set(v) =
            if _seekValue <> v then
                _seekValue <- v
                notify "SeekValue"
                if _isSeeking then
                    let snap = _playback.Snapshot()
                    if snap.Duration.TotalSeconds > 0.0 then
                        let pos = TimeSpan.FromSeconds(v / 1000.0 * snap.Duration.TotalSeconds)
                        _playback.Seek(pos)

    member _.IsSeeking
        with get() = _isSeeking
        and set(v) = if _isSeeking <> v then _isSeeking <- v; notify "IsSeeking"

    member _.ControlsVisible
        with get() = _controlsVisible
        and set(v) = if _controlsVisible <> v then _controlsVisible <- v; notify "ControlsVisible"

    member _.VideoRenderer
        with get() = _videoRenderer
        and set(v) =
            if _videoRenderer <> v then
                _videoRenderer <- v
                notify "VideoRenderer"
                notify "UseNativeVideoHost"
                notify "UseOpenGlVideoSurface"
                notify "VideoRendererLabel"

    member _.UseNativeVideoHost =
        _hasMedia && not _isAudioMode && _videoRenderer = VideoRendererKind.NativeVulkan && OperatingSystem.IsWindows()

    member self.UseOpenGlVideoSurface =
        _hasMedia && not _isAudioMode && not self.UseNativeVideoHost

    member _.VideoRendererLabel =
        if _videoRenderer = VideoRendererKind.NativeVulkan then "Vulkan (native)" else "OpenGL"

    member _.IsAlwaysOnTop
        with get() = _isAlwaysOnTop
        and set(v) = if _isAlwaysOnTop <> v then _isAlwaysOnTop <- v; notify "IsAlwaysOnTop"

    member _.IsPlaylistVisible
        with get() = _isPlaylistVisible
        and set(v) = if _isPlaylistVisible <> v then _isPlaylistVisible <- v; notify "IsPlaylistVisible"

    member _.HasVideoAdjustments = _hasVideoAdjustments
    member _.CurrentSubtitleText = _currentSubtitleText
    member _.SubtitleFontSizeValue = _subtitleFontSize
    member _.SubtitleFontFamily = "Inter"
    member _.SubtitleForeground = Brushes.White
    member _.IsAudioMode = _isAudioMode
    member _.IsAudioOnly = _isAudioMode
    member _.CoverArtBitmap = null
    member _.HasCoverArt = false
    member _.TrackTitle =
        match _playback.CurrentFilePath with
        | path when not (String.IsNullOrWhiteSpace(path)) -> Path.GetFileNameWithoutExtension(path)
        | _ -> ""
    member _.TrackArtistAlbum = ""
    member _.HasFolderTracks = _playlistItems.Count > 1
    member _.FolderTrackLabel = self.PlaylistPositionLabel
    member _.HasMedia = _hasMedia
    member _.HasRecentFiles = _recentFileItems.Count > 0
    member _.RecentFiles = _settings.RecentFiles
    member _.RecentFileItems = _recentFileItems
    member _.ErrorMessage = _errorMessage |> Option.toObj
    member _.HasError = _errorMessage.IsSome
    member _.OsdMessage = _osdMessage
    member _.HasOsd = not (String.IsNullOrEmpty(_osdMessage))
    member _.IsCasting = _isCasting
    member _.CastStatusText = _castStatusText
    member _.IsCastPlaying = _isCastPlaying
    member _.ControlsOpacity = _controlsOpacity
    member _.SeekStepLabel = _seekStepLabel
    member _.ChapterPositions = _chapterPositions
    member _.PlaylistItems = _playlistItems
    member _.PlaylistCount = _playlistItems.Count
    member _.HasPlaylist = _playlistItems.Count > 0
    member _.PlaylistPositionLabel =
        if _playlistItems.Count = 0 || _playlistIndex < 0 then "0 / 0"
        else $"{_playlistIndex + 1} / {_playlistItems.Count}"
    member _.AudioTracks =
        if isNull _playback then [||]
        else
            _playback.GetAudioTracks()
            |> Array.map (fun t -> TrackInfo(t.Id, t.Name, t.IsSelected))
    member _.SubtitleTracks =
        if isNull _playback then [||]
        else
            _playback.GetSubtitleTracks()
            |> Array.map (fun t -> TrackInfo(t.Id, t.Name, t.IsSelected))

    member _.TogglePlayPauseCommand = RelayCommand(fun _ -> _playback.TogglePlayPause())
    member _.ToggleMuteCommand = RelayCommand(fun _ -> _playback.ToggleMute())
    member _.TogglePlaylistCommand = RelayCommand(fun _ -> self.IsPlaylistVisible <- not self.IsPlaylistVisible)
    member _.ToggleAlwaysOnTopCommand = RelayCommand(fun _ -> self.IsAlwaysOnTop <- not self.IsAlwaysOnTop)
    member _.SeekBackwardCommand = RelayCommand(fun _ -> _playback.SeekRelative(TimeSpan.FromSeconds(-float _settings.SeekStep)))
    member _.SeekForwardCommand = RelayCommand(fun _ -> _playback.SeekRelative(TimeSpan.FromSeconds(float _settings.SeekStep)))
    member _.StopCommand = RelayCommand(fun _ -> _playback.Stop())
    member _.PreviousTrackCommand = RelayCommand(fun _ -> openAtIndex(_playlistIndex - 1) |> ignore)
    member _.NextTrackCommand = RelayCommand(fun _ -> openAtIndex(_playlistIndex + 1) |> ignore)
    member self.ResetSpeedCommand = RelayCommand(fun _ -> (self.SetSpeedCommand :> ICommand).Execute("1.0"))
    member _.VolumeUpCommand = RelayCommand(fun _ -> self.Volume <- self.Volume + 5)
    member _.VolumeDownCommand = RelayCommand(fun _ -> self.Volume <- self.Volume - 5)
    member self.SpeedUpCommand = RelayCommand(fun _ -> (self.SetSpeedCommand :> ICommand).Execute(_speed + 0.25f))
    member self.SpeedDownCommand = RelayCommand(fun _ -> (self.SetSpeedCommand :> ICommand).Execute(_speed - 0.25f))
    member _.SeekBackward30Command = RelayCommand(fun _ -> _playback.SeekRelative(TimeSpan.FromSeconds(-30.0)))
    member _.SeekForward30Command = RelayCommand(fun _ -> _playback.SeekRelative(TimeSpan.FromSeconds(30.0)))
    member _.StepFrameCommand = RelayCommand(fun _ -> _playback.StepFrame())
    member _.StepFrameBackCommand = RelayCommand(fun _ -> _playback.StepFrameBack())
    member _.PreviousChapterCommand = RelayCommand(fun _ -> _playback.SeekToChapter(-1))
    member _.NextChapterCommand = RelayCommand(fun _ -> _playback.SeekToChapter(1))
    member _.CycleSeekStepCommand =
        RelayCommand(fun _ ->
            _seekStep <-
                match _seekStep with
                | 5 -> 10
                | 10 -> 30
                | _ -> 5
            _seekStepLabel <- $"{_seekStep}s"
            _settings.SaveSessionPreferences(_volume, _speed, _seekStep)
            notify "SeekStepLabel")
    member _.ToggleLoopCommand = RelayCommand(fun _ -> _playback.ToggleLoop())
    member _.TakeScreenshotCommand =
        RelayCommand(fun _ ->
            if not (String.IsNullOrWhiteSpace(_playback.CurrentFilePath)) then
                let dir =
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                    |> fun path -> if String.IsNullOrWhiteSpace(path) then AppContext.BaseDirectory else path
                let fileName = "ImPlay-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png"
                _playback.TakeSnapshot(Path.Combine(dir, fileName)) |> ignore)
    member _.ClearPlaylistCommand =
        RelayCommand(fun _ ->
            _playlistPaths.Clear()
            _playlistIndex <- -1
            rebuildPlaylistItems())
    member _.RemoveRecentFileCommand =
        RelayCommand(fun parameter ->
            match parameter with
            | :? string as path ->
                _settings.RemoveRecentFile(path)
                notify "RecentFiles"
                notify "HasRecentFiles"
            | _ -> ())
    member _.OpenPlaylistItemCommand =
        RelayCommand(fun parameter ->
            match parameter with
            | :? int as index -> openAtIndex(index) |> ignore
            | _ -> ())
    member _.RemovePlaylistItemCommand =
        RelayCommand(fun parameter ->
            match parameter with
            | :? int as index when index >= 0 && index < _playlistPaths.Count ->
                _playlistPaths.RemoveAt(index)
                if _playlistPaths.Count = 0 then _playlistIndex <- -1
                elif _playlistIndex >= _playlistPaths.Count then _playlistIndex <- _playlistPaths.Count - 1
                rebuildPlaylistItems()
            | _ -> ())
    member _.SetSpeedCommand =
        RelayCommand(fun parameter ->
            let rate =
                match parameter with
                | :? float32 as value -> value
                | :? float as value -> float32 value
                | :? string as text ->
                    match Single.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture) with
                    | true, value -> value
                    | _ -> _speed
                | _ -> _speed

            let clamped = Math.Clamp(rate, 0.25f, 4.0f)
            _playback.SetSpeed(clamped)
            _settings.SaveSessionPreferences(_volume, clamped, _settings.SeekStep)
            _speed <- clamped
            notify "Speed"
            notify "SpeedLabel"
            notify "IsNormalSpeed")

    member _.SetAudioTrackCommand =
        RelayCommand(fun parameter ->
            match parameter with
            | :? int as id ->
                _playback.SetAudioTrack(id)
                notify "AudioTracks"
            | _ -> ())

    member _.SetSubtitleTrackCommand =
        RelayCommand(fun parameter ->
            match parameter with
            | :? int as id ->
                _playback.SetSubtitleTrack(id)
                notify "SubtitleTracks"
            | _ -> ())

    member _.CycleAudioTrackCommand =
        RelayCommand(fun _ ->
            _playback.CycleAudioTrack()
            notify "AudioTracks")

    member _.CycleSubtitleTrackCommand =
        RelayCommand(fun _ ->
            _playback.CycleSubtitleTrack()
            notify "SubtitleTracks")

    member _.SetVideoRendererCommand =
        RelayCommand(fun parameter ->
            let renderer =
                match parameter with
                | :? VideoRendererKind as value -> value
                | :? string as text ->
                    match Enum.TryParse<VideoRendererKind>(text, true) with
                    | true, value -> value
                    | _ -> _videoRenderer
                | _ -> _videoRenderer

            let changed = renderer <> _videoRenderer
            self.VideoRenderer <- renderer
            _settings.SaveVideoRenderer(renderer)
            if not (isNull _playback) then
                _playback.ShutdownRenderer()
            if not changed then
                notify "UseNativeVideoHost"
                notify "UseOpenGlVideoSurface"
                notify "VideoRendererLabel")

    member _.ToggleVideoRendererCommand =
        RelayCommand(fun _ ->
            let next =
                if _videoRenderer = VideoRendererKind.NativeVulkan then VideoRendererKind.OpenGl
                else VideoRendererKind.NativeVulkan
            (self.SetVideoRendererCommand :> ICommand).Execute(next))

    member _.StopCastingCommand = RelayCommand(fun _ -> ())
    member _.ToggleCastPlaybackCommand = RelayCommand(fun _ -> ())
    member _.CastVolumeDownCommand = RelayCommand(fun _ -> ())
    member _.CastVolumeUpCommand = RelayCommand(fun _ -> ())

    member _.PausePlayback() = _playback.Pause()
    member _.ResumePlayback() = _playback.Play()
    member _.JumpTo(position: TimeSpan) = _playback.Seek(position)
    member _.CommitSeek(value: double) =
        let snap = _playback.Snapshot()
        if snap.Duration.TotalSeconds > 0.0 then
            _playback.Seek(TimeSpan.FromSeconds(value / 1000.0 * snap.Duration.TotalSeconds))
    member _.RefreshTracksNow() =
        notify "AudioTracks"
        notify "SubtitleTracks"
    
    member _.OpenAsync(filePath: string) =
        if String.IsNullOrWhiteSpace(filePath) then ()
        else
            _playlistPaths.Clear()
            _playlistPaths.Add(filePath)
            _playlistIndex <- 0
            rebuildPlaylistItems()
            openAtIndex(0) |> ignore

    member _.OpenFileAsync(filePath: string) =
        task {
            if not (String.IsNullOrWhiteSpace(filePath)) then
                _playlistPaths.Clear()
                _playlistPaths.Add(filePath)
                _playlistIndex <- 0
                rebuildPlaylistItems()
                do! openAtIndex(0)
        } :> Task

    member _.LoadFilesAsync(filePaths: IEnumerable<string>) =
        task {
            let paths =
                filePaths
                |> Seq.filter (fun path -> not (String.IsNullOrWhiteSpace(path)))
                |> Seq.toList
            if paths.Length > 0 then
                _playlistPaths.Clear()
                for path in paths do _playlistPaths.Add(path)
                _playlistIndex <- 0
                rebuildPlaylistItems()
                do! openAtIndex(0)
        } :> Task

    member _.AddFilesAsync(filePaths: IEnumerable<string>) =
        task {
            for path in filePaths do
                if not (String.IsNullOrWhiteSpace(path)) && not (_playlistPaths.Contains(path)) then
                    _playlistPaths.Add(path)
            rebuildPlaylistItems()
            if _playlistIndex < 0 && _playlistPaths.Count > 0 then
                do! openAtIndex(0)
        } :> Task

    member _.LoadSubtitleFileAsync(path: string) =
        task {
            if not (String.IsNullOrWhiteSpace(path)) then
                _playback.LoadSubtitleFile(path)
                notify "SubtitleTracks"
        } :> Task
        
    interface IDisposable with
        member _.Dispose() =
            (_playback :> IDisposable).Dispose()
