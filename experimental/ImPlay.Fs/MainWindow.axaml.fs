namespace ImPlay.Fs

open System
open System.Globalization
open System.IO
open System.Threading.Tasks
open Avalonia
open Avalonia.Collections
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Markup.Xaml
open Avalonia.Platform.Storage
open Avalonia.Media
open HanumanInstitute.LibMpv
open HanumanInstitute.LibMpv.Avalonia
open ImPlay.Fs.Domain
open System.Collections.Generic
open System.Diagnostics
open Avalonia.Styling
open Avalonia.Layout

type MainWindow(startupPaths: string list) as this =
    inherit Window()

    let mutable mpvView: MpvView = null
    let mutable btnOpen: Button = null
    let mutable btnOpenFolder: Button = null
    let mutable btnPlay: Button = null
    let mutable btnPrev: Button = null
    let mutable btnNext: Button = null
    let mutable btnSeekBack: Button = null
    let mutable btnSeekForward: Button = null
    let mutable btnChapterPrev: Button = null
    let mutable btnChapterNext: Button = null
    let mutable btnFrameStep: Button = null
    let mutable btnFrameBack: Button = null
    let mutable btnStop: Button = null
    let mutable btnToggleSidebar: Button = null
    let mutable btnMute: Button = null
    let mutable btnFullscreen: Button = null
    let mutable btnLoopA: Button = null
    let mutable btnLoopB: Button = null
    let mutable btnLoopClear: Button = null
    let mutable sliderVolume: Slider = null
    let mutable sliderSeek: Slider = null
    let mutable txtStatus: TextBlock = null
    let mutable txtTimePos: TextBlock = null
    let mutable txtTimeRemaining: TextBlock = null
    let mutable sidebar: Border = null
    let mutable listRecent: ListBox = null
    let mutable listPlaylist: ListBox = null
    let mutable btnPlaylistClear: Button = null
    let mutable btnPlaylistShuffle: Button = null
    let mutable btnPlaylistSort: Button = null
    let mutable btnPlaylistSave: Button = null
    let mutable menuRecent: MenuItem = null
    let mutable menuPlaylist: MenuItem = null
    let mutable menuPlaylistPlay: MenuItem = null
    let mutable menuPlaylistMoveUp: MenuItem = null
    let mutable menuPlaylistMoveDown: MenuItem = null
    let mutable menuPlaylistRemove: MenuItem = null
    let mutable menuPlaylistReveal: MenuItem = null
    let mutable menuChapters: MenuItem = null
    let mutable menuAudio: MenuItem = null
    let mutable menuVideo: MenuItem = null
    let mutable menuSubtitle: MenuItem = null
    let mutable menuProfiles: MenuItem = null
    let mutable menuThemes: MenuItem = null
    let mutable menuAbLoop: MenuItem = null
    let mutable menuFileLoop: MenuItem = null
    let mutable menuPlaylistLoop: MenuItem = null
    let mutable menuFrameStep: MenuItem = null
    let mutable menuFrameBack: MenuItem = null
    let mutable menuPlaylistNext: MenuItem = null
    let mutable menuPlaylistPrev: MenuItem = null
    let mutable menuChapterNext: MenuItem = null
    let mutable menuChapterPrev: MenuItem = null
    let mutable menuPlaylistPalette: MenuItem = null
    let mutable menuChapterPalette: MenuItem = null
    let mutable menuHistoryPalette: MenuItem = null
    let mutable menuSpeed10: MenuItem = null
    let mutable menuSpeed12: MenuItem = null
    let mutable menuSpeed15: MenuItem = null
    let mutable menuSpeed20: MenuItem = null
    let mutable menuSpeed07: MenuItem = null
    let mutable menuSpeed05: MenuItem = null
    
    let mutable listChapters: ListBox = null
    let mutable listVideoTracks: ListBox = null
    let mutable listAudioTracks: ListBox = null
    let mutable listSubTracks: ListBox = null
    let mutable listSubTracks2: ListBox = null
    let mutable sliderBrightness: Slider = null
    let mutable sliderContrast: Slider = null
    let mutable sliderSaturation: Slider = null
    let mutable sliderAudioDelay: Slider = null
    let mutable sliderSubScale: Slider = null
    let mutable sliderSubDelay: Slider = null
    let mutable eqSliders: Slider[] = [||]
    let mutable btnEqReset: Button = null
    let mutable chkSubVisible: CheckBox = null
    let mutable menuSeekForward10: MenuItem = null
    let mutable menuSeekForward60: MenuItem = null
    let mutable menuSeekBack10: MenuItem = null
    let mutable menuSeekBack60: MenuItem = null
    
    let mutable menuPlay: MenuItem = null
    let mutable menuStop: MenuItem = null
    let mutable menuOpenFiles: MenuItem = null
    let mutable menuOpenFolder: MenuItem = null
    let mutable menuFullscreen: MenuItem = null
    let mutable menuScreenshot: MenuItem = null
    let mutable menuSettings: MenuItem = null
    let mutable menuAbout: MenuItem = null
    let mutable menuQuit: MenuItem = null
    let mutable menuOpenUrl: MenuItem = null
    let mutable menuOpenDisk: MenuItem = null
    let mutable menuOpenIso: MenuItem = null
    let mutable menuOpenClipboard: MenuItem = null
    let mutable menuAudioTracks: MenuItem = null
    let mutable menuAudioDevices: MenuItem = null
    let mutable menuAudioDelayInc: MenuItem = null
    let mutable menuAudioDelayDec: MenuItem = null
    let mutable menuVideoTracks: MenuItem = null
    let mutable menuVideoRotate0: MenuItem = null
    let mutable menuVideoRotate90: MenuItem = null
    let mutable menuVideoRotate180: MenuItem = null
    let mutable menuVideoRotate270: MenuItem = null
    let mutable menuVideoScale200: MenuItem = null
    let mutable menuVideoScale150: MenuItem = null
    let mutable menuVideoScale100: MenuItem = null
    let mutable menuVideoScale75: MenuItem = null
    let mutable menuVideoScale50: MenuItem = null
    let mutable menuVideoScale25: MenuItem = null
    let mutable menuVideoScaleInc: MenuItem = null
    let mutable menuVideoScaleDec: MenuItem = null
    let mutable menuVideoPanscanInc: MenuItem = null
    let mutable menuVideoPanscanDec: MenuItem = null
    let mutable menuVideoPanReset: MenuItem = null
    let mutable menuVideoAspect16_9: MenuItem = null
    let mutable menuVideoAspect16_10: MenuItem = null
    let mutable menuVideoAspect4_3: MenuItem = null
    let mutable menuVideoAspect235: MenuItem = null
    let mutable menuVideoAspect185: MenuItem = null
    let mutable menuVideoAspect1: MenuItem = null
    let mutable menuVideoAspectReset: MenuItem = null
    let mutable menuVideoQual4320: MenuItem = null
    let mutable menuVideoQual2160: MenuItem = null
    let mutable menuVideoQual1440: MenuItem = null
    let mutable menuVideoQual1080: MenuItem = null
    let mutable menuVideoQual720: MenuItem = null
    let mutable menuVideoQual480: MenuItem = null
    let mutable menuVideoHwdec: MenuItem = null
    let mutable menuVideoDeinterlace: MenuItem = null
    let mutable menuSubTracks: MenuItem = null
    let mutable menuSubSecondary: MenuItem = null
    let mutable menuSubLoad: MenuItem = null
    let mutable menuSubVisibility: MenuItem = null
    let mutable menuSubPosUp: MenuItem = null
    let mutable menuSubPosDown: MenuItem = null
    let mutable menuSubDelayInc: MenuItem = null
    let mutable menuSubDelayDec: MenuItem = null
    let mutable menuOnTop: MenuItem = null
    let mutable menuWindowBorder: MenuItem = null
    let mutable menuOscVisibility: MenuItem = null
    let mutable menuOpenConfigDir: MenuItem = null
    let mutable menuWindowDragging: MenuItem = null
    let mutable menuShowProgress: MenuItem = null
    let mutable menuShowStats: MenuItem = null
    let mutable menuQuit: MenuItem = null
    let mutable menuQuitWatchLater: MenuItem = null
    let mutable menuAudioEqDefault: MenuItem = null
    let mutable menuAudioEqRock: MenuItem = null
    let mutable menuAudioEqPop: MenuItem = null
    let mutable menuAudioEqClassical: MenuItem = null
    let mutable menuAudioEqTechno: MenuItem = null
    let mutable menuAudioEqSoft: MenuItem = null
    let mutable menuIconPlay: PathIcon = null
    let mutable menuCopyPath: MenuItem = null
    let mutable menuRevealCurrent: MenuItem = null
    let mutable menuDebug: MenuItem = null
    let mutable debugWin: DebugWindow option = None

    let mutable iconPlayPause: PathIcon = null
    let mutable iconMuteUnmute: PathIcon = null
    let mutable overlayNotification: Border = null
    let mutable txtNotification: TextBlock = null

    let chaptersSource = AvaloniaList<ChapterItem>()
    let videoTracksSource = AvaloniaList<TrackItem>()
    let audioTracksSource = AvaloniaList<TrackItem>()
    let subTracksSource = AvaloniaList<TrackItem>()

    let mutable overlayPalette: Grid = null
    let mutable txtPaletteInput: TextBox = null
    let mutable listPaletteMatches: ListBox = null

    let paletteItems = List<CommandItem>()
    let paletteMatchesSource = AvaloniaList<CommandItem>()
    let shadersSource = AvaloniaList<string>()

    let mutable currentConfig = Config.loadConfig()
    let mutable playerState = Domain.initialState
    let startupArgs = Cli.parse startupPaths
    let recentFilesSource = AvaloniaList<RecentItem>(currentConfig.RecentFiles)
    let playlistSource = AvaloniaList<PlaylistItem>()
    let mutable playlistPaths: string list = []
    let mutable currentPath: string option = None
    let mutable currentIndex = -1
    let mutable isPaused = false
    let mutable isMuted = false
    let mutable isFullscreen = false
    let mutable duration = 0.0
    let mutable isSeeking = false
    let mutable lastSeekPercent = Double.NaN
    let mutable dragItemIndex = -1
    let mutable isInternalDrag = false

    do
        StartupLog.writef "MainWindow constructor entered; startupPaths=%A" startupPaths
        Lang.setLang currentConfig.Interface.Lang
        StartupLog.write "MainWindow language set"
        this.InitializeComponent()
        StartupLog.write "MainWindow InitializeComponent returned"
        this.ApplyConfig()
        StartupLog.write "MainWindow ApplyConfig returned"

    new() = MainWindow([])

    member private this.FormatTime(seconds: float) =
        let totalSeconds =
            if Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds < 0.0 then
                0
            else
                int (Math.Floor seconds)

        let hours = totalSeconds / 3600
        let minutes = (totalSeconds % 3600) / 60
        let secs = totalSeconds % 60

        if hours > 0 then
            sprintf "%d:%02d:%02d" hours minutes secs
        else
            sprintf "%02d:%02d" minutes secs

    member private this.UpdateTimecodes() =
        let rawPercent = if sliderSeek <> null then sliderSeek.Value else 0.0
        let percent = rawPercent |> max 0.0 |> min 100.0
        let current =
            if duration > 0.0 then
                duration * percent / 100.0
            else
                0.0
        if txtTimePos <> null then
            txtTimePos.Text <- this.FormatTime current
        if txtTimeRemaining <> null then
            let remaining = if duration > 0.0 then duration - current else 0.0
            txtTimeRemaining.Text <- "-" + this.FormatTime remaining

    member private this.TryMpvCommand(args: string[], ?label: string) =
        let label = defaultArg label (if args.Length > 0 then args.[0] else "command")
        if mpvView = null || mpvView.MpvContext = null then
            StartupLog.writef "mpv command skipped; no context: %s [%s]" label (String.concat " " args)
            false
        else
            try
                StartupLog.writef "mpv command: %s [%s]" label (String.concat " " args)
                mpvView.MpvContext.RunCommand(MpvCommandOptions(), args)
                true
            with ex ->
                StartupLog.writef "mpv command failed: %s [%s]: %s" label (String.concat " " args) ex.Message
                if txtStatus <> null then
                    txtStatus.Text <- sprintf "mpv command failed: %s" label
                false

    member private this.TryMpvSetProperty(name: string, value: string, ?label: string) =
        let label = defaultArg label (sprintf "set %s" name)
        if mpvView = null || mpvView.MpvContext = null then
            StartupLog.writef "mpv property skipped; no context: %s=%s" name value
            false
        else
            try
                StartupLog.writef "mpv set property: %s=%s" name value
                mpvView.MpvContext.SetProperty(name, value)
                true
            with ex ->
                StartupLog.writef "mpv set property failed: %s=%s: %s" name value ex.Message
                if txtStatus <> null then
                    txtStatus.Text <- sprintf "mpv property failed: %s" label
                false

    member private this.TryMpvSetBoolProperty(name: string, value: bool, ?label: string) =
        let label = defaultArg label (sprintf "set %s" name)
        if mpvView = null || mpvView.MpvContext = null then
            StartupLog.writef "mpv bool property skipped; no context: %s=%b" name value
            false
        else
            try
                StartupLog.writef "mpv set bool property: %s=%b" name value
                match name with
                | "pause" -> mpvView.MpvContext.Pause.Set(value)
                | "mute" -> mpvView.MpvContext.Mute.Set(value)
                | "fullscreen" -> mpvView.MpvContext.Fullscreen.Set(value)
                | _ -> mpvView.MpvContext.SetProperty(name, if value then "yes" else "no")
                true
            with ex ->
                StartupLog.writef "mpv set bool property failed: %s=%b: %s" name value ex.Message
                if txtStatus <> null then
                    txtStatus.Text <- sprintf "mpv property failed: %s" label
                false

    member private this.SeekAbsolutePercent(percent: float, exact: bool) =
        let target = percent |> max 0.0 |> min 100.0
        let targetText = target.ToString("0.###", CultureInfo.InvariantCulture)
        if duration > 0.0 then
            let seconds = duration * target / 100.0
            let secondsText = seconds.ToString("0.###", CultureInfo.InvariantCulture)
            this.TryMpvSetProperty("time-pos", secondsText, "seek time-pos") |> ignore
        else
            this.TryMpvSetProperty("percent-pos", targetText, "seek percent-pos") |> ignore

    member private this.SeekAbsolutePercentIfChanged(percent: float, exact: bool) =
        let target = percent |> max 0.0 |> min 100.0
        if exact || Double.IsNaN(lastSeekPercent) || Math.Abs(target - lastSeekPercent) >= 0.25 then
            lastSeekPercent <- target
            this.SeekAbsolutePercent(target, exact)

    member private this.SetSeekSliderFromPointer(e: PointerEventArgs) =
        if sliderSeek <> null && sliderSeek.Bounds.Width > 0.0 then
            let pos = e.GetPosition(sliderSeek)
            let percent = (pos.X / sliderSeek.Bounds.Width * 100.0) |> max 0.0 |> min 100.0
            sliderSeek.Value <- percent
            this.UpdateTimecodes()
            percent
        else
            sliderSeek.Value

    member private this.UpdateSeekFromPercent(percent: float) =
        if sliderSeek <> null && not isSeeking then
            sliderSeek.Value <- percent |> max 0.0 |> min 100.0
            sliderSeek.IsEnabled <- true
            this.UpdateTimecodes()

    member private this.SeekRelative(seconds: float) =
        let targetText = seconds.ToString("0.###", CultureInfo.InvariantCulture)
        this.TryMpvCommand([| "seek"; targetText; "relative"; "keyframes" |], "seek relative") |> ignore

    member private this.InitializeComponent() =
        StartupLog.write "MainWindow.InitializeComponent loading XAML"
        AvaloniaXamlLoader.Load(this)
        StartupLog.write "MainWindow.InitializeComponent XAML loaded"
        
        mpvView <- this.FindControl<MpvView>("MpvPlayer")
        btnOpen <- this.FindControl<Button>("BtnOpen")
        btnOpenFolder <- this.FindControl<Button>("BtnOpenFolder")
        btnPlay <- this.FindControl<Button>("BtnPlay")
        btnPrev <- this.FindControl<Button>("BtnPrev")
        btnNext <- this.FindControl<Button>("BtnNext")
        btnSeekBack <- this.FindControl<Button>("BtnSeekBack")
        btnSeekForward <- this.FindControl<Button>("BtnSeekForward")
        btnChapterPrev <- this.FindControl<Button>("BtnChapterPrev")
        btnChapterNext <- this.FindControl<Button>("BtnChapterNext")
        btnFrameStep <- this.FindControl<Button>("BtnFrameStep")
        btnFrameBack <- this.FindControl<Button>("BtnFrameBack")
        btnStop <- this.FindControl<Button>("BtnStop")
        btnToggleSidebar <- this.FindControl<Button>("BtnToggleSidebar")
        btnMute <- this.FindControl<Button>("BtnMute")
        btnFullscreen <- this.FindControl<Button>("BtnFullscreen")
        btnLoopA <- this.FindControl<Button>("BtnLoopA")
        btnLoopB <- this.FindControl<Button>("BtnLoopB")
        btnLoopClear <- this.FindControl<Button>("BtnLoopClear")
        sliderVolume <- this.FindControl<Slider>("SliderVolume")
        sliderSeek <- this.FindControl<Slider>("SliderSeek")
        txtStatus <- this.FindControl<TextBlock>("TxtStatus")
        txtTimePos <- this.FindControl<TextBlock>("TxtTimePos")
        txtTimeRemaining <- this.FindControl<TextBlock>("TxtTimeRemaining")
        sidebar <- this.FindControl<Border>("Sidebar")
        listRecent <- this.FindControl<ListBox>("ListRecent")
        let btnClearRecent = this.FindControl<Button>("BtnClearRecent")
        listPlaylist <- this.FindControl<ListBox>("ListPlaylist")
        btnPlaylistClear <- this.FindControl<Button>("BtnPlaylistClear")
        btnPlaylistShuffle <- this.FindControl<Button>("BtnPlaylistShuffle")
        btnPlaylistSort <- this.FindControl<Button>("BtnPlaylistSort")
        btnPlaylistSave <- this.FindControl<Button>("BtnPlaylistSave")

        // Quick Settings Controls
        listChapters <- this.FindControl<ListBox>("ListChapters")
        listVideoTracks <- this.FindControl<ListBox>("ListVideoTracks")
        listAudioTracks <- this.FindControl<ListBox>("ListAudioTracks")
        listSubTracks <- this.FindControl<ListBox>("ListSubTracks")
        listSubTracks2 <- this.FindControl<ListBox>("ListSubTracks2")
        sliderBrightness <- this.FindControl<Slider>("SliderBrightness")
        sliderContrast <- this.FindControl<Slider>("SliderContrast")
        sliderSaturation <- this.FindControl<Slider>("SliderSaturation")
        sliderAudioDelay <- this.FindControl<Slider>("SliderAudioDelay")
        sliderSubScale <- this.FindControl<Slider>("SliderSubScale")
        sliderSubDelay <- this.FindControl<Slider>("SliderSubDelay")
        chkSubVisible <- this.FindControl<CheckBox>("ChkSubVisible")

        let listShaders = this.FindControl<ListBox>("ListShaders")
        listShaders.ItemsSource <- shadersSource
        let btnAddShader = this.FindControl<Button>("BtnAddShader")
        let btnClearShaders = this.FindControl<Button>("BtnClearShaders")

        btnAddShader.Click.Add(fun _ ->
            task {
                let options = FilePickerOpenOptions(Title = "Add GLSL Shader")
                options.FileTypeFilter <- [ FilePickerFileType("GLSL Shaders", Patterns = [ "*.glsl"; "*.hook" ]) ]
                let! files = this.StorageProvider.OpenFilePickerAsync(options)
                if not (Seq.isEmpty files) then
                    let path = this.StorageItemToPath (Seq.head files)
                    if not (String.IsNullOrWhiteSpace path) then
                        if mpvView <> null && mpvView.MpvContext <> null then
                            this.TryMpvCommand([| "change-list"; "glsl-shaders"; "append"; path |], "shader add") |> ignore
                            this.RefreshShaders()
            } |> ignore
        )

        btnClearShaders.Click.Add(fun _ ->
            if this.TryMpvSetProperty("glsl-shaders", "") then
                this.RefreshShaders()
        )

        btnEqReset <- this.FindControl<Button>("BtnEqReset")
        eqSliders <- [|
            this.FindControl<Slider>("Eq31")
            this.FindControl<Slider>("Eq63")
            this.FindControl<Slider>("Eq125")
            this.FindControl<Slider>("Eq250")
            this.FindControl<Slider>("Eq500")
            this.FindControl<Slider>("Eq1k")
            this.FindControl<Slider>("Eq2k")
            this.FindControl<Slider>("Eq4k")
            this.FindControl<Slider>("Eq8k")
            this.FindControl<Slider>("Eq16k")
        |]

        iconPlayPause <- this.FindControl<PathIcon>("IconPlayPause")
        iconMuteUnmute <- this.FindControl<PathIcon>("IconMuteUnmute")
        overlayNotification <- this.FindControl<Border>("NotificationOverlay")
        txtNotification <- this.FindControl<TextBlock>("TxtNotification")

        listChapters.ItemsSource <- chaptersSource
        listVideoTracks.ItemsSource <- videoTracksSource
        listAudioTracks.ItemsSource <- audioTracksSource
        listSubTracks.ItemsSource <- subTracksSource
        listSubTracks2.ItemsSource <- subTracksSource

        overlayPalette <- this.FindControl<Grid>("CommandPaletteOverlay")
        txtPaletteInput <- this.FindControl<TextBox>("TxtCommandInput")
        listPaletteMatches <- this.FindControl<ListBox>("ListCommandMatches")
        listPaletteMatches.ItemsSource <- paletteMatchesSource

        txtPaletteInput.GetObservable(TextBox.TextProperty).Subscribe(fun t -> this.FilterPalette(t)) |> ignore
        
        txtPaletteInput.KeyDown.Add(fun e ->
            if e.Key = Key.Escape then
                this.HidePalette()
            elif e.Key = Key.Enter then
                if listPaletteMatches.SelectedIndex >= 0 then
                    this.ExecutePaletteItem(paletteMatchesSource.[listPaletteMatches.SelectedIndex])
                elif paletteMatchesSource.Count > 0 then
                    this.ExecutePaletteItem(paletteMatchesSource.[0])
            elif e.Key = Key.Down then
                listPaletteMatches.SelectedIndex <- (listPaletteMatches.SelectedIndex + 1) % paletteMatchesSource.Count
            elif e.Key = Key.Up then
                listPaletteMatches.SelectedIndex <- (listPaletteMatches.SelectedIndex - 1 + paletteMatchesSource.Count) % paletteMatchesSource.Count
        )

        listPaletteMatches.DoubleTapped.Add(fun _ ->
            if listPaletteMatches.SelectedIndex >= 0 then
                this.ExecutePaletteItem(paletteMatchesSource.[listPaletteMatches.SelectedIndex])
        )

        this.KeyDown.Add(fun e ->
            if e.Key = Key.P && e.KeyModifiers = KeyModifiers.Control then
                this.ShowPalette("playlist")
            elif e.Key = Key.P && e.KeyModifiers = (KeyModifiers.Control ||| KeyModifiers.Shift) then
                this.ShowPalette("bindings")
            elif e.Key = Key.H && e.KeyModifiers = KeyModifiers.Control then
                this.ShowPalette("history")
            elif e.Key = Key.C && e.KeyModifiers = KeyModifiers.Control then
                this.ShowPalette("chapters")
            else
                this.HandleKey(e)
        )

        // Context Menu Items
        menuPlay <- this.FindControl<MenuItem>("MenuPlay")
        menuStop <- this.FindControl<MenuItem>("MenuStop")
        menuOpenFiles <- this.FindControl<MenuItem>("MenuOpenFiles")
        menuOpenFolder <- this.FindControl<MenuItem>("MenuOpenFolder")
        menuRecent <- this.FindControl<MenuItem>("MenuRecent")
        menuPlaylist <- this.FindControl<MenuItem>("MenuPlaylist")
        menuPlaylistPlay <- this.FindControl<MenuItem>("MenuPlaylistPlay")
        menuPlaylistMoveUp <- this.FindControl<MenuItem>("MenuPlaylistMoveUp")
        menuPlaylistMoveDown <- this.FindControl<MenuItem>("MenuPlaylistMoveDown")
        menuPlaylistRemove <- this.FindControl<MenuItem>("MenuPlaylistRemove")
        menuPlaylistReveal <- this.FindControl<MenuItem>("MenuPlaylistReveal")
        menuChapters <- this.FindControl<MenuItem>("MenuChapters")
        menuAudio <- this.FindControl<MenuItem>("MenuAudio")
        menuVideo <- this.FindControl<MenuItem>("MenuVideo")
        menuSubtitle <- this.FindControl<MenuItem>("MenuSubtitle")
        menuProfiles <- this.FindControl<MenuItem>("MenuProfiles")
        menuThemes <- this.FindControl<MenuItem>("MenuThemes")
        menuDebug <- this.FindControl<MenuItem>("MenuDebug")
        menuAbLoop <- this.FindControl<MenuItem>("MenuAbLoop")
        menuFileLoop <- this.FindControl<MenuItem>("MenuFileLoop")
        menuPlaylistLoop <- this.FindControl<MenuItem>("MenuPlaylistLoop")
        menuFrameStep <- this.FindControl<MenuItem>("MenuFrameStep")
        menuFrameBack <- this.FindControl<MenuItem>("MenuFrameBack")
        menuPlaylistNext <- this.FindControl<MenuItem>("MenuPlaylistNext")
        menuPlaylistPrev <- this.FindControl<MenuItem>("MenuPlaylistPrev")
        menuChapterNext <- this.FindControl<MenuItem>("MenuChapterNext")
        menuChapterPrev <- this.FindControl<MenuItem>("MenuChapterPrev")
        menuPlaylistPalette <- this.FindControl<MenuItem>("MenuPlaylistPalette")
        menuChapterPalette <- this.FindControl<MenuItem>("MenuChapterPalette")
        menuHistoryPalette <- this.FindControl<MenuItem>("MenuHistoryPalette")
        menuSpeed10 <- this.FindControl<MenuItem>("MenuSpeed10")
        menuSpeed12 <- this.FindControl<MenuItem>("MenuSpeed12")
        menuSpeed15 <- this.FindControl<MenuItem>("MenuSpeed15")
        menuSpeed20 <- this.FindControl<MenuItem>("MenuSpeed20")
        menuSpeed07 <- this.FindControl<MenuItem>("MenuSpeed07")
        menuSpeed05 <- this.FindControl<MenuItem>("MenuSpeed05")
        menuFullscreen <- this.FindControl<MenuItem>("MenuFullscreen")
        menuScreenshot <- this.FindControl<MenuItem>("MenuScreenshot")
        menuSettings <- this.FindControl<MenuItem>("MenuSettings")
        menuAbout <- this.FindControl<MenuItem>("MenuAbout")
        menuQuit <- this.FindControl<MenuItem>("MenuQuit")
        menuSeekForward10 <- this.FindControl<MenuItem>("MenuSeekForward10")
        menuSeekForward60 <- this.FindControl<MenuItem>("MenuSeekForward60")
        menuSeekBack10 <- this.FindControl<MenuItem>("MenuSeekBack10")
        menuSeekBack60 <- this.FindControl<MenuItem>("MenuSeekBack60")
        menuOpenUrl <- this.FindControl<MenuItem>("MenuOpenUrl")
        menuOpenDisk <- this.FindControl<MenuItem>("MenuOpenDisk")
        menuOpenIso <- this.FindControl<MenuItem>("MenuOpenIso")
        menuOpenClipboard <- this.FindControl<MenuItem>("MenuOpenClipboard")
        
        menuAudioTracks <- this.FindControl<MenuItem>("MenuAudioTracks")
        menuAudioDevices <- this.FindControl<MenuItem>("MenuAudioDevices")
        menuAudioDelayInc <- this.FindControl<MenuItem>("MenuAudioDelayInc")
        menuAudioDelayDec <- this.FindControl<MenuItem>("MenuAudioDelayDec")
        
        menuVideoTracks <- this.FindControl<MenuItem>("MenuVideoTracks")
        menuVideoRotate0 <- this.FindControl<MenuItem>("MenuVideoRotate0")
        menuVideoRotate90 <- this.FindControl<MenuItem>("MenuVideoRotate90")
        menuVideoRotate180 <- this.FindControl<MenuItem>("MenuVideoRotate180")
        menuVideoRotate270 <- this.FindControl<MenuItem>("MenuVideoRotate270")
        menuVideoScale200 <- this.FindControl<MenuItem>("MenuVideoScale200")
        menuVideoScale150 <- this.FindControl<MenuItem>("MenuVideoScale150")
        menuVideoScale100 <- this.FindControl<MenuItem>("MenuVideoScale100")
        menuVideoScale75 <- this.FindControl<MenuItem>("MenuVideoScale75")
        menuVideoScale50 <- this.FindControl<MenuItem>("MenuVideoScale50")
        menuVideoScale25 <- this.FindControl<MenuItem>("MenuVideoScale25")
        menuVideoScaleInc <- this.FindControl<MenuItem>("MenuVideoScaleInc")
        menuVideoScaleDec <- this.FindControl<MenuItem>("MenuVideoScaleDec")
        menuVideoPanscanInc <- this.FindControl<MenuItem>("MenuVideoPanscanInc")
        menuVideoPanscanDec <- this.FindControl<MenuItem>("MenuVideoPanscanDec")
        menuVideoPanReset <- this.FindControl<MenuItem>("MenuVideoPanReset")
        menuVideoAspect16_9 <- this.FindControl<MenuItem>("MenuVideoAspect16_9")
        menuVideoAspect16_10 <- this.FindControl<MenuItem>("MenuVideoAspect16_10")
        menuVideoAspect4_3 <- this.FindControl<MenuItem>("MenuVideoAspect4_3")
        menuVideoAspect235 <- this.FindControl<MenuItem>("MenuVideoAspect235")
        menuVideoAspect185 <- this.FindControl<MenuItem>("MenuVideoAspect185")
        menuVideoAspect1 <- this.FindControl<MenuItem>("MenuVideoAspect1")
        menuVideoAspectReset <- this.FindControl<MenuItem>("MenuVideoAspectReset")
        menuVideoQual4320 <- this.FindControl<MenuItem>("MenuVideoQual4320")
        menuVideoQual2160 <- this.FindControl<MenuItem>("MenuVideoQual2160")
        menuVideoQual1440 <- this.FindControl<MenuItem>("MenuVideoQual1440")
        menuVideoQual1080 <- this.FindControl<MenuItem>("MenuVideoQual1080")
        menuVideoQual720 <- this.FindControl<MenuItem>("MenuVideoQual720")
        menuVideoQual480 <- this.FindControl<MenuItem>("MenuVideoQual480")
        menuVideoHwdec <- this.FindControl<MenuItem>("MenuVideoHwdec")
        menuVideoDeinterlace <- this.FindControl<MenuItem>("MenuVideoDeinterlace")
        
        menuSubTracks <- this.FindControl<MenuItem>("MenuSubTracks")
        menuSubSecondary <- this.FindControl<MenuItem>("MenuSubSecondary")
        menuSubLoad <- this.FindControl<MenuItem>("MenuSubLoad")
        menuSubVisibility <- this.FindControl<MenuItem>("MenuSubVisibility")
        menuSubPosUp <- this.FindControl<MenuItem>("MenuSubPosUp")
        menuSubPosDown <- this.FindControl<MenuItem>("MenuSubPosDown")
        menuSubDelayInc <- this.FindControl<MenuItem>("MenuSubDelayInc")
        menuSubDelayDec <- this.FindControl<MenuItem>("MenuSubDelayDec")
        
        menuOnTop <- this.FindControl<MenuItem>("MenuOnTop")
        menuWindowBorder <- this.FindControl<MenuItem>("MenuWindowBorder")
        menuWindowDragging <- this.FindControl<MenuItem>("MenuWindowDragging")
        menuShowProgress <- this.FindControl<MenuItem>("MenuShowProgress")
        menuShowStats <- this.FindControl<MenuItem>("MenuShowStats")
        menuOscVisibility <- this.FindControl<MenuItem>("MenuOscVisibility")
        menuOpenConfigDir <- this.FindControl<MenuItem>("MenuOpenConfigDir")
        menuQuitWatchLater <- this.FindControl<MenuItem>("MenuQuitWatchLater")
        menuQuit <- this.FindControl<MenuItem>("MenuQuit")
        menuDebug <- this.FindControl<MenuItem>("MenuDebug")
        menuCopyPath <- this.FindControl<MenuItem>("MenuCopyPath")
        menuRevealCurrent <- this.FindControl<MenuItem>("MenuRevealCurrent")
        menuIconPlay <- this.FindControl<PathIcon>("MenuIconPlay")
        
        menuAudioEqDefault <- this.FindControl<MenuItem>("MenuAudioEqDefault")
        menuAudioEqRock <- this.FindControl<MenuItem>("MenuAudioEqRock")
        menuAudioEqPop <- this.FindControl<MenuItem>("MenuAudioEqPop")
        menuAudioEqClassical <- this.FindControl<MenuItem>("MenuAudioEqClassical")
        menuAudioEqTechno <- this.FindControl<MenuItem>("MenuAudioEqTechno")
        menuAudioEqSoft <- this.FindControl<MenuItem>("MenuAudioEqSoft")
        
        let mainContextMenu = this.FindControl<ContextMenu>("MainContextMenu")

        // Localize menu headers
        menuPlay.Header <- Lang.i18n "menu.play"
        menuStop.Header <- Lang.i18n "menu.stop"
        menuOpenFiles.Header <- Lang.i18n "menu.open.files"
        menuOpenFolder.Header <- Lang.i18n "menu.open.folder"
        menuRecent.Header <- Lang.i18n "menu.open.recent"
        menuPlaylist.Header <- Lang.i18n "menu.playlist"
        menuChapters.Header <- Lang.i18n "menu.chapters"
        menuAudio.Header <- Lang.i18n "menu.audio"
        menuVideo.Header <- Lang.i18n "menu.video"
        menuSubtitle.Header <- Lang.i18n "menu.subtitle"
        menuProfiles.Header <- Lang.i18n "menu.tools.profiles"
        menuThemes.Header <- Lang.i18n "menu.tools.theme"
        menuDebug.Header <- Lang.i18n "menu.tools.debug"
        menuSettings.Header <- Lang.i18n "menu.settings"
        menuAbout.Header <- Lang.i18n "menu.about"
        menuQuit.Header <- Lang.i18n "menu.quit"
        menuQuitWatchLater.Header <- "Quit (Watch Later)"
        menuWindowDragging.Header <- "Window Dragging"
        menuShowProgress.Header <- "Show Progress"
        menuShowStats.Header <- "Show Statistics"

        menuQuitWatchLater.Click.Add(fun _ -> 
            if this.TryMpvCommand([| "quit-watch-later" |], "quit watch later") then
                this.Close()
        )
        
        menuWindowDragging.Click.Add(fun _ -> this.TryMpvCommand([| "cycle"; "window-dragging" |], "window dragging") |> ignore)
        menuShowProgress.Click.Add(fun _ -> this.TryMpvCommand([| "show-progress" |], "show progress") |> ignore)
        menuShowStats.Click.Add(fun _ -> this.TryMpvCommand([| "script-binding"; "stats/display-stats-toggle" |], "show stats") |> ignore)

        let setAudioEq (gains: float[]) =
            eqSliders |> Array.iteri (fun i s -> s.Value <- gains.[i])
            this.UpdateEqualizer()
            this.ShowNotification("Equalizer preset applied")

        menuAudioEqDefault.Click.Add(fun _ -> setAudioEq [| 0.0; 0.0; 0.0; 0.0; 0.0; 0.0; 0.0; 0.0; 0.0; 0.0 |])
        menuAudioEqRock.Click.Add(fun _ -> setAudioEq [| 4.0; 3.0; 2.0; 0.0; -1.0; -1.0; 0.0; 2.0; 3.0; 4.0 |])
        menuAudioEqPop.Click.Add(fun _ -> setAudioEq [| -1.0; 0.0; 2.0; 3.0; 3.0; 2.0; 0.0; -1.0; -1.0; -1.0 |])
        menuAudioEqClassical.Click.Add(fun _ -> setAudioEq [| 3.0; 2.0; 2.0; 1.0; 0.0; 0.0; 1.0; 2.0; 2.0; 3.0 |])
        menuAudioEqTechno.Click.Add(fun _ -> setAudioEq [| 4.0; 3.0; 0.0; -2.0; -2.0; 0.0; 3.0; 4.0; 4.0; 4.0 |])
        menuAudioEqSoft.Click.Add(fun _ -> setAudioEq [| 2.0; 1.0; 0.0; -1.0; -1.0; 0.0; 1.0; 2.0; 3.0; 4.0 |])
        
        menuOpenUrl.Header <- Lang.i18n "menu.open.url"
        menuOpenDisk.Header <- Lang.i18n "menu.open.disk"
        menuOpenIso.Header <- Lang.i18n "menu.open.iso"
        menuOpenClipboard.Header <- Lang.i18n "menu.open.clipboard"

        menuPlaylistPlay.Click.Add(fun _ ->
            let index = listPlaylist.SelectedIndex
            if index >= 0 then
                this.TryMpvCommand([| "playlist-play-index"; string index |], "playlist play index") |> ignore
        )
        menuPlaylistMoveUp.Click.Add(fun _ ->
            let index = listPlaylist.SelectedIndex
            if index > 0 then
                this.TryMpvCommand([| "playlist-move"; string index; string (index - 1) |], "playlist move up") |> ignore
                listPlaylist.SelectedIndex <- index - 1
        )
        menuPlaylistMoveDown.Click.Add(fun _ ->
            let index = listPlaylist.SelectedIndex
            if index >= 0 && index < playlistPaths.Length - 1 then
                this.TryMpvCommand([| "playlist-move"; string index; string (index + 2) |], "playlist move down") |> ignore
                listPlaylist.SelectedIndex <- index + 1
        )
        menuPlaylistRemove.Click.Add(fun _ ->
            let index = listPlaylist.SelectedIndex
            if index >= 0 then
                this.TryMpvCommand([| "playlist-remove"; string index |], "playlist remove") |> ignore
        )
        menuPlaylistReveal.Click.Add(fun _ ->
            let index = listPlaylist.SelectedIndex
            if index >= 0 && index < playlistPaths.Length then
                let path = playlistPaths.[index]
                if File.Exists(path) then
                    Process.Start(ProcessStartInfo(FileName = "explorer.exe", Arguments = sprintf "/select,\"%s\"" path, UseShellExecute = true)) |> ignore
        )

        menuAbLoop.Click.Add(fun _ -> this.TryMpvCommand([| "ab-loop" |], "ab loop") |> ignore)
        menuFileLoop.Click.Add(fun _ -> this.TryMpvCommand([| "cycle-values"; "loop-file"; "inf"; "no" |], "file loop") |> ignore)
        menuPlaylistLoop.Click.Add(fun _ -> this.TryMpvCommand([| "cycle-values"; "loop-playlist"; "inf"; "no" |], "playlist loop") |> ignore)
        menuFrameStep.Click.Add(fun _ -> this.TryMpvCommand([| "frame-step" |], "frame step") |> ignore)
        menuFrameBack.Click.Add(fun _ -> this.TryMpvCommand([| "frame-back-step" |], "frame back") |> ignore)
        menuPlaylistNext.Click.Add(fun _ -> this.TryMpvCommand([| "playlist-next" |], "playlist next") |> ignore)
        menuPlaylistPrev.Click.Add(fun _ -> this.TryMpvCommand([| "playlist-prev" |], "playlist previous") |> ignore)
        menuChapterNext.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "chapter"; "1" |], "chapter next") |> ignore)
        menuChapterPrev.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "chapter"; "-1" |], "chapter previous") |> ignore)
        
        menuPlaylistPalette.Click.Add(fun _ -> this.ShowPalette("playlist"))
        menuChapterPalette.Click.Add(fun _ -> this.ShowPalette("chapters"))
        if menuHistoryPalette <> null then
            menuHistoryPalette.Click.Add(fun _ -> this.ShowPalette("history"))
        
        let setSpeed s = this.TryMpvCommand([| "set"; "speed"; string s |], "set speed") |> ignore
        menuSpeed10.Click.Add(fun _ -> setSpeed 1.0)
        menuSpeed12.Click.Add(fun _ -> setSpeed 1.25)
        menuSpeed15.Click.Add(fun _ -> setSpeed 1.5)
        menuSpeed20.Click.Add(fun _ -> setSpeed 2.0)
        menuSpeed07.Click.Add(fun _ -> setSpeed 0.75)
        menuSpeed05.Click.Add(fun _ -> setSpeed 0.5)

        menuSeekForward10.Click.Add(fun _ -> this.SeekRelative(10.0))
        menuSeekForward60.Click.Add(fun _ -> this.SeekRelative(60.0))
        menuSeekBack10.Click.Add(fun _ -> this.SeekRelative(-10.0))
        menuSeekBack60.Click.Add(fun _ -> this.SeekRelative(-60.0))

        menuDebug.Click.Add(fun _ ->
            match debugWin with
            | None ->
                let win = DebugWindow.Show(this, mpvView.MpvContext)
                debugWin <- Some win
                win.Closed.Add(fun _ -> debugWin <- None)
            | Some win ->
                win.Activate()
        )

        mainContextMenu.Opened.Add(fun _ -> this.PopulateDynamicMenus())

        menuPlay.Click.Add(fun _ -> btnPlay.RaiseEvent(RoutedEventArgs(Button.ClickEvent)))
        menuStop.Click.Add(fun _ -> btnStop.RaiseEvent(RoutedEventArgs(Button.ClickEvent)))
        menuOpenFiles.Click.Add(fun _ -> btnOpen.RaiseEvent(RoutedEventArgs(Button.ClickEvent)))
        menuOpenFolder.Click.Add(fun _ -> btnOpenFolder.RaiseEvent(RoutedEventArgs(Button.ClickEvent)))
        
        menuOpenClipboard.Click.Add(fun _ -> this.OpenFromClipboard() |> ignore)
        menuOpenUrl.Click.Add(fun _ -> this.OpenUrl() |> ignore)
        menuOpenDisk.Click.Add(fun _ -> this.OpenDisk() |> ignore)
        menuOpenIso.Click.Add(fun _ -> this.OpenIso() |> ignore)
        
        menuAudioDelayInc.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "audio-delay"; "0.1" |], "audio delay inc") |> ignore)
        menuAudioDelayDec.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "audio-delay"; "-0.1" |], "audio delay dec") |> ignore)
        
        menuVideoRotate0.Click.Add(fun _ -> this.TryMpvSetProperty("video-rotate", "0") |> ignore)
        menuVideoRotate90.Click.Add(fun _ -> this.TryMpvSetProperty("video-rotate", "90") |> ignore)
        menuVideoRotate180.Click.Add(fun _ -> this.TryMpvSetProperty("video-rotate", "180") |> ignore)
        menuVideoRotate270.Click.Add(fun _ -> this.TryMpvSetProperty("video-rotate", "270") |> ignore)
        
        let setScale s = this.TryMpvSetProperty("window-scale", string s) |> ignore
        menuVideoScale200.Click.Add(fun _ -> setScale 2.0)
        menuVideoScale150.Click.Add(fun _ -> setScale 1.5)
        menuVideoScale100.Click.Add(fun _ -> setScale 1.0)
        menuVideoScale75.Click.Add(fun _ -> setScale 0.75)
        menuVideoScale50.Click.Add(fun _ -> setScale 0.5)
        menuVideoScale25.Click.Add(fun _ -> setScale 0.25)
        menuVideoScaleInc.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "window-scale"; "0.1" |], "window scale inc") |> ignore)
        menuVideoScaleDec.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "window-scale"; "-0.1" |], "window scale dec") |> ignore)
        
        menuVideoPanscanInc.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "panscan"; "0.1" |], "panscan inc") |> ignore)
        menuVideoPanscanDec.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "panscan"; "-0.1" |], "panscan dec") |> ignore)
        menuVideoPanReset.Click.Add(fun _ -> 
            this.TryMpvSetProperty("video-zoom", "0") |> ignore
            this.TryMpvSetProperty("panscan", "0") |> ignore
            this.TryMpvSetProperty("video-pan-x", "0") |> ignore
            this.TryMpvSetProperty("video-pan-y", "0") |> ignore
        )
        
        let setAspect a = this.TryMpvSetProperty("video-aspect-override", a) |> ignore
        menuVideoAspect16_9.Click.Add(fun _ -> setAspect "16:9")
        menuVideoAspect16_10.Click.Add(fun _ -> setAspect "16:10")
        menuVideoAspect4_3.Click.Add(fun _ -> setAspect "4:3")
        menuVideoAspect235.Click.Add(fun _ -> setAspect "2.35:1")
        menuVideoAspect185.Click.Add(fun _ -> setAspect "1.85:1")
        menuVideoAspect1.Click.Add(fun _ -> setAspect "1:1")
        menuVideoAspectReset.Click.Add(fun _ -> setAspect "-1")
        
        let setQual q = this.TryMpvSetProperty("ytdl-format", sprintf "bv*[height<=%s]+ba/b[height<=%s]" q q) |> ignore
        menuVideoQual4320.Click.Add(fun _ -> setQual "4320")
        menuVideoQual2160.Click.Add(fun _ -> setQual "2160")
        menuVideoQual1440.Click.Add(fun _ -> setQual "1440")
        menuVideoQual1080.Click.Add(fun _ -> setQual "1080")
        menuVideoQual720.Click.Add(fun _ -> setQual "720")
        menuVideoQual480.Click.Add(fun _ -> setQual "480")
        
        menuVideoHwdec.Click.Add(fun _ -> this.TryMpvCommand([| "cycle-values"; "hwdec"; "auto"; "no" |], "hwdec toggle") |> ignore)
        menuVideoDeinterlace.Click.Add(fun _ -> this.TryMpvCommand([| "cycle"; "deinterlace" |], "deinterlace toggle") |> ignore)
        
        menuSubLoad.Click.Add(fun _ ->
            task {
                let options = FilePickerOpenOptions(Title = "Load Subtitle")
                options.FileTypeFilter <- [ Media.subtitleFilter ]
                let! files = this.StorageProvider.OpenFilePickerAsync(options)
                if not (Seq.isEmpty files) then
                    let path = this.StorageItemToPath (Seq.head files)
                    if not (String.IsNullOrWhiteSpace path) then
                        this.AddSubtitle(path, false)
            } |> ignore
        )
        menuSubVisibility.Click.Add(fun _ -> this.TryMpvCommand([| "cycle"; "sub-visibility" |], "subtitle visibility") |> ignore)
        menuSubPosUp.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "sub-pos"; "-1" |], "subtitle position up") |> ignore)
        menuSubPosDown.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "sub-pos"; "1" |], "subtitle position down") |> ignore)
        menuSubDelayInc.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "sub-delay"; "0.1" |], "subtitle delay inc") |> ignore)
        menuSubDelayDec.Click.Add(fun _ -> this.TryMpvCommand([| "add"; "sub-delay"; "-0.1" |], "subtitle delay dec") |> ignore)
        
        menuOnTop.Click.Add(fun _ -> 
            this.Topmost <- not this.Topmost
            this.ShowNotification(if this.Topmost then "Stay on Top: On" else "Stay on Top: Off")
        )
        menuWindowBorder.Click.Add(fun _ -> this.TryMpvCommand([| "cycle"; "border" |], "window border") |> ignore)
        menuOscVisibility.Click.Add(fun _ -> this.TryMpvCommand([| "script-binding"; "osc/visibility" |], "osc visibility") |> ignore)
        menuOpenConfigDir.Click.Add(fun _ ->
            let path = Platform.getBundledConfigPath()
            if Directory.Exists(path) then
                Process.Start(ProcessStartInfo(FileName = "explorer.exe", Arguments = path, UseShellExecute = true)) |> ignore
        )
        
        menuQuitWatchLater.Click.Add(fun _ ->
            if this.TryMpvCommand([| "quit-watch-later" |], "quit watch later") then
                this.Close()
        )
        
        menuQuit.Click.Add(fun _ -> this.Close())
        
        menuCopyPath.Click.Add(fun _ ->
            if currentPath.IsSome then
                this.Clipboard.SetTextAsync(currentPath.Value) |> ignore
                this.ShowNotification("Path copied to clipboard")
        )
        
        menuRevealCurrent.Click.Add(fun _ ->
            if currentPath.IsSome then
                let path = currentPath.Value
                if File.Exists(path) || Directory.Exists(path) then
                    Process.Start(ProcessStartInfo(FileName = "explorer.exe", Arguments = sprintf "/select,\"%s\"" path, UseShellExecute = true)) |> ignore
        )


        menuFullscreen.Click.Add(fun _ -> btnFullscreen.RaiseEvent(RoutedEventArgs(Button.ClickEvent)))
        
        menuScreenshot.Click.Add(fun _ ->
            if this.TryMpvCommand([| "async"; "screenshot" |], "screenshot") then
                this.ShowNotification(Lang.i18n "notification.screenshot.saved")
        )

        menuSettings.Click.Add(fun _ ->
            task {
                let! result = SettingsWindow.ShowDialog(this, currentConfig)
                match result with
                | Some newConfig ->
                    currentConfig <- newConfig
                    Config.saveConfig currentConfig
                    txtStatus.Text <- "Settings saved. Some changes may require restart."
                | None -> ()
            } |> ignore
        )
        menuAbout.Click.Add(fun _ -> txtStatus.Text <- "ImPlay Modern (F# Prototype) v0.1.0")
        menuQuit.Click.Add(fun _ -> this.Close())
        
        let btnPlaylistClear = this.FindControl<Button>("BtnPlaylistClear")
        let btnPlaylistShuffle = this.FindControl<Button>("BtnPlaylistShuffle")
        let btnRotate0 = this.FindControl<Button>("BtnRotate0")
        let btnRotate90 = this.FindControl<Button>("BtnRotate90")
        let btnRotate180 = this.FindControl<Button>("BtnRotate180")
        let btnRotate270 = this.FindControl<Button>("BtnRotate270")

        // Quick Settings Handlers
        btnPlaylistClear.Click.Add(fun _ -> this.ClearPlaylist())
        btnPlaylistShuffle.Click.Add(fun _ ->
            this.TryMpvCommand([| "playlist-shuffle" |], "playlist shuffle") |> ignore
        )
        let mutable sortReverse = false
        btnPlaylistSort.Click.Add(fun _ ->
            this.PlaylistSort(sortReverse)
            sortReverse <- not sortReverse
        )
        btnPlaylistSave.Click.Add(fun _ -> this.SavePlaylistAsync() |> ignore)
        btnClearRecent.Click.Add(fun _ ->
            currentConfig <- { currentConfig with RecentFiles = [] }
            Config.saveConfig currentConfig
            recentFilesSource.Clear()
            this.ShowNotification("History cleared")
        )
        btnFullscreen.Click.Add(fun _ ->
            this.TryMpvCommand([| "cycle"; "fullscreen" |], "fullscreen toggle") |> ignore
        )
        btnLoopA.Click.Add(fun _ ->
            if this.TryMpvCommand([| "ab-loop" |], "ab loop a") then
                this.ShowNotification("A-B Loop Set A")
        )
        btnLoopB.Click.Add(fun _ ->
            if this.TryMpvCommand([| "ab-loop" |], "ab loop b") then
                this.ShowNotification("A-B Loop Set B / Play")
        )
        btnLoopClear.Click.Add(fun _ ->
            let clearedA = this.TryMpvSetProperty("ab-loop-a", "no")
            let clearedB = this.TryMpvSetProperty("ab-loop-b", "no")
            if clearedA && clearedB then
                this.ShowNotification("A-B Loop Cleared")
        )
        btnPrev.Click.Add(fun _ ->
            this.TryMpvCommand([| "playlist-prev"; "weak" |], "playlist previous weak") |> ignore
        )
        btnNext.Click.Add(fun _ ->
            this.TryMpvCommand([| "playlist-next"; "weak" |], "playlist next weak") |> ignore
        )
        if btnChapterPrev <> null then
            btnChapterPrev.Click.Add(fun _ ->
                this.TryMpvCommand([| "add"; "chapter"; "-1" |], "chapter previous") |> ignore
            )
        if btnChapterNext <> null then
            btnChapterNext.Click.Add(fun _ ->
                this.TryMpvCommand([| "add"; "chapter"; "1" |], "chapter next") |> ignore
            )
        if btnSeekBack <> null then
            btnSeekBack.Click.Add(fun _ ->
                this.SeekRelative(-5.0)
            )
        if btnSeekForward <> null then
            btnSeekForward.Click.Add(fun _ ->
                this.SeekRelative(10.0)
            )
        btnFrameStep.Click.Add(fun _ ->
            this.TryMpvCommand([| "frame-step" |], "frame step") |> ignore
        )
        btnFrameBack.Click.Add(fun _ ->
            this.TryMpvCommand([| "frame-back-step" |], "frame back") |> ignore
        )

        let setRotate deg =
            this.TryMpvSetProperty("video-rotate", deg) |> ignore
        
        if btnRotate0 <> null then btnRotate0.Click.Add(fun _ -> setRotate "0")
        if btnRotate90 <> null then btnRotate90.Click.Add(fun _ -> setRotate "90")
        if btnRotate180 <> null then btnRotate180.Click.Add(fun _ -> setRotate "180")
        if btnRotate270 <> null then btnRotate270.Click.Add(fun _ -> setRotate "270")

        let setEq prop (valObj: obj) =
            this.TryMpvSetProperty(prop, string valObj) |> ignore

        sliderBrightness.GetObservable(Slider.ValueProperty).Subscribe(fun v -> setEq "brightness" v) |> ignore
        sliderContrast.GetObservable(Slider.ValueProperty).Subscribe(fun v -> setEq "contrast" v) |> ignore
        sliderSaturation.GetObservable(Slider.ValueProperty).Subscribe(fun v -> setEq "saturation" v) |> ignore
        sliderAudioDelay.GetObservable(Slider.ValueProperty).Subscribe(fun v -> setEq "audio-delay" v) |> ignore
        sliderSubScale.GetObservable(Slider.ValueProperty).Subscribe(fun v -> setEq "sub-scale" v) |> ignore
        sliderSubDelay.GetObservable(Slider.ValueProperty).Subscribe(fun v -> setEq "sub-delay" v) |> ignore
        
        eqSliders |> Array.iteri (fun i s ->
            s.GetObservable(Slider.ValueProperty).Subscribe(fun _ -> this.UpdateEqualizer()) |> ignore
        )
        if btnEqReset <> null then
            btnEqReset.Click.Add(fun _ ->
                eqSliders |> Array.iter (fun s -> s.Value <- 0.0)
                this.UpdateEqualizer()
            )
        
        chkSubVisible.IsCheckedChanged.Add(fun _ ->
            this.TryMpvSetProperty("sub-visibility", if chkSubVisible.IsChecked.HasValue && chkSubVisible.IsChecked.Value then "yes" else "no") |> ignore
        )

        listChapters.SelectionChanged.Add(fun args ->
            if args.AddedItems.Count > 0 then
                let item = args.AddedItems.[0] :?> ChapterItem
                this.TryMpvCommand([| "set"; "chapter"; string item.Id |], "chapter select") |> ignore
        )

        let handleTrackSelect (args: SelectionChangedEventArgs) prop =
            if args.AddedItems.Count > 0 then
                let item = args.AddedItems.[0] :?> TrackItem
                this.TryMpvSetProperty(prop, string item.Id, sprintf "select %s" prop) |> ignore

        listVideoTracks.SelectionChanged.Add(fun args -> handleTrackSelect args "vid")
        listAudioTracks.SelectionChanged.Add(fun args -> handleTrackSelect args "aid")
        listSubTracks.SelectionChanged.Add(fun args -> handleTrackSelect args "sid")
        listSubTracks2.SelectionChanged.Add(fun args -> handleTrackSelect args "secondary-sid")

        DragDrop.SetAllowDrop(this, true)
        this.AddHandler<DragEventArgs>(
            DragDrop.DragOverEvent,
            fun _ args ->
                args.DragEffects <- DragDropEffects.Copy
                args.Handled <- true
        )
        this.AddHandler<DragEventArgs>(
            DragDrop.DropEvent,
            fun _ args ->
                let dropped = this.GetDroppedPaths(args)
                if not (List.isEmpty dropped) then
                    this.LoadPaths(dropped, false)
                args.Handled <- true
        )

        // Playlist Internal Drag-and-Drop
        listPlaylist.PointerPressed.Add(fun args ->
            let item = listPlaylist.SelectedIndex
            if item >= 0 then
                dragItemIndex <- item
                isInternalDrag <- true
                let data = DataObject()
                data.Set("PlaylistIndex", item)
                task {
                    let! _ = DragDrop.DoDragDrop(args, data, DragDropEffects.Move)
                    isInternalDrag <- false
                    dragItemIndex <- -1
                } |> ignore
        )

        DragDrop.SetAllowDrop(listPlaylist, true)
        listPlaylist.AddHandler<DragEventArgs>(
            DragDrop.DragOverEvent,
            fun sender args ->
                if isInternalDrag then
                    args.DragEffects <- DragDropEffects.Move
                    args.Handled <- true
                else
                    args.DragEffects <- DragDropEffects.Copy
                    args.Handled <- true
        )

        listPlaylist.AddHandler<DragEventArgs>(
            DragDrop.DropEvent,
            fun _ args ->
                if isInternalDrag then
                    if dragItemIndex >= 0 then
                        // Use hit testing to find the drop target index
                        let pos = args.GetPosition(listPlaylist)
                        let targetItem = listPlaylist.InputHitTest(pos)
                        
                        // This is a bit simplified, ideally we find the exact index.
                        // For now, let's use the current selection if it updated, 
                        // or just move it to the end/start if we can't find it.
                        // Better: just move to the currently selected index which should update on drop click
                        let targetIndex = listPlaylist.SelectedIndex
                        if targetIndex >= 0 && targetIndex <> dragItemIndex then
                            let target = if targetIndex > dragItemIndex then targetIndex + 1 else targetIndex
                            this.TryMpvCommand([| "playlist-move"; string dragItemIndex; string target |], "playlist drag move") |> ignore
                    args.Handled <- true
                else
                    let dropped = this.GetDroppedPaths(args)
                    if not (List.isEmpty dropped) then
                        this.LoadPaths(dropped, true)
                    args.Handled <- true
        )

        listRecent.ItemsSource <- recentFilesSource
        listPlaylist.ItemsSource <- playlistSource
        listVideoTracks.ItemsSource <- videoTracksSource
        listAudioTracks.ItemsSource <- audioTracksSource
        listSubTracks.ItemsSource <- subTracksSource
        listSubTracks2.ItemsSource <- subTracksSource
        listChapters.ItemsSource <- chaptersSource

        sliderVolume.Value <- float currentConfig.Mpv.Volume

        btnOpen.Click.Add(fun _ ->
            this.OpenFilesAsync() |> ignore
        )

        btnOpenFolder.Click.Add(fun _ ->
            this.OpenFolderAsync() |> ignore
        )

        btnPlay.Click.Add(fun _ ->
            if mpvView <> null && mpvView.MpvContext <> null then
                let count = this.GetPropertyInt("playlist-count")
                if count > 0 then
                    isPaused <- not isPaused
                    this.TryMpvSetBoolProperty("pause", isPaused) |> ignore
                elif not currentConfig.RecentFiles.IsEmpty then
                    this.LoadPaths([ currentConfig.RecentFiles.Head.Path ], false)
                else
                    this.OpenFilesAsync() |> ignore
        )

        btnStop.Click.Add(fun _ ->
            if mpvView <> null && mpvView.MpvContext <> null then
                this.TryMpvCommand([| "stop" |], "stop") |> ignore
                currentPath <- None
                isPaused <- false
                if iconPlayPause <> null then iconPlayPause.Data <- this.FindResource("IconPlay") :?> StreamGeometry
                txtStatus.Text <- "Stopped"
                duration <- 0.0
                sliderSeek.Maximum <- 100.0
                sliderSeek.Value <- 0.0
                sliderSeek.IsEnabled <- false
                this.UpdateTimecodes()
        )

        btnMute.Click.Add(fun _ ->
            if mpvView <> null && mpvView.MpvContext <> null then
                isMuted <- not isMuted
                this.TryMpvSetBoolProperty("mute", isMuted) |> ignore
                btnMute.Content <- if isMuted then "Mute" else "Vol"
        )

        btnFullscreen.Click.Add(fun _ ->
            isFullscreen <- not isFullscreen
            this.WindowState <- if isFullscreen then WindowState.FullScreen else WindowState.Normal
            this.TryMpvSetBoolProperty("fullscreen", isFullscreen) |> ignore
        )

        btnToggleSidebar.Click.Add(fun _ ->
            sidebar.IsVisible <- not sidebar.IsVisible
        )

        // Playlist Context Menu Handlers
        let menuPlaylistPlay = this.FindControl<MenuItem>("MenuPlaylistPlay")
        let menuPlaylistMoveUp = this.FindControl<MenuItem>("MenuPlaylistMoveUp")
        let menuPlaylistMoveDown = this.FindControl<MenuItem>("MenuPlaylistMoveDown")
        let menuPlaylistRemove = this.FindControl<MenuItem>("MenuPlaylistRemove")
        let menuPlaylistReveal = this.FindControl<MenuItem>("MenuPlaylistReveal")

        menuPlaylistPlay.Header <- Lang.i18n "views.quickview.playlist.menu.play"
        menuPlaylistMoveUp.Header <- Lang.i18n "views.quickview.playlist.menu.move_up"
        menuPlaylistMoveDown.Header <- Lang.i18n "views.quickview.playlist.menu.move_down"
        menuPlaylistRemove.Header <- Lang.i18n "views.quickview.playlist.menu.remove"
        menuPlaylistReveal.Header <- Lang.i18n "views.quickview.playlist.menu.reveal"

        menuPlaylistPlay.Click.Add(fun _ -> 
            if listPlaylist.SelectedIndex >= 0 then this.PlayPlaylistIndex(listPlaylist.SelectedIndex))
        menuPlaylistMoveUp.Click.Add(fun _ ->
            if listPlaylist.SelectedIndex > 0 then
                let idx = listPlaylist.SelectedIndex
                this.TryMpvCommand([| "playlist-move"; string idx; string (idx - 1) |], "playlist move up") |> ignore)
        menuPlaylistMoveDown.Click.Add(fun _ ->
            if listPlaylist.SelectedIndex >= 0 && listPlaylist.SelectedIndex < playlistPaths.Length - 1 then
                let idx = listPlaylist.SelectedIndex
                this.TryMpvCommand([| "playlist-move"; string (idx + 1); string idx |], "playlist move down") |> ignore)
        menuPlaylistRemove.Click.Add(fun _ ->
            if listPlaylist.SelectedIndex >= 0 then
                this.TryMpvCommand([| "playlist-remove"; string listPlaylist.SelectedIndex |], "playlist remove") |> ignore)
        menuPlaylistReveal.Click.Add(fun _ ->
            if listPlaylist.SelectedIndex >= 0 then
                let path = playlistPaths.[listPlaylist.SelectedIndex]
                try
                    Process.Start(ProcessStartInfo(FileName = "explorer.exe", Arguments = sprintf "/select,\"%s\"" path, UseShellExecute = true)) |> ignore
                with _ -> ())

        sliderVolume.PropertyChanged.Add(fun args ->
            if args.Property.Name = "Value" && mpvView <> null && mpvView.MpvContext <> null then
                let volume = args.NewValue :?> float
                this.TryMpvSetProperty("volume", volume.ToString("0.###", CultureInfo.InvariantCulture)) |> ignore
                currentConfig <- { currentConfig with Mpv = { currentConfig.Mpv with Volume = int volume } }
                Config.saveConfig currentConfig
        )

        sliderSeek.AddHandler(Slider.PointerPressedEvent, (fun _ e ->
            isSeeking <- true
            lastSeekPercent <- Double.NaN
            e.Pointer.Capture(sliderSeek) |> ignore
            let percent = this.SetSeekSliderFromPointer(e)
            this.SeekAbsolutePercentIfChanged(percent, true)
            e.Handled <- true
        ), RoutingStrategies.Tunnel)
        sliderSeek.AddHandler(Slider.PointerMovedEvent, (fun _ e ->
            if isSeeking then
                let point = e.GetCurrentPoint(sliderSeek)
                if point.Properties.IsLeftButtonPressed then
                    let percent = this.SetSeekSliderFromPointer(e)
                    this.SeekAbsolutePercentIfChanged(percent, false)
                    e.Handled <- true
                else
                    isSeeking <- false
                    lastSeekPercent <- Double.NaN
                    e.Pointer.Capture(null) |> ignore
        ), RoutingStrategies.Tunnel)
        sliderSeek.AddHandler(Slider.PointerReleasedEvent, (fun _ e -> 
            let percent = this.SetSeekSliderFromPointer(e)
            isSeeking <- false
            this.SeekAbsolutePercentIfChanged(percent, true)
            lastSeekPercent <- Double.NaN
            this.UpdateTimecodes()
            e.Pointer.Capture(null) |> ignore
            e.Handled <- true
        ), RoutingStrategies.Tunnel)

        sliderSeek.PropertyChanged.Add(fun args ->
            if args.Property.Name = "Value" && isSeeking then
                this.UpdateTimecodes()
        )

        listRecent.SelectionChanged.Add(fun args ->
            if args.AddedItems.Count > 0 then
                let item = args.AddedItems.[0] :?> RecentItem
                this.LoadPaths([ item.Path ], false)
        )

        listPlaylist.DoubleTapped.Add(fun _ ->
            if listPlaylist.SelectedIndex >= 0 then
                let item = listPlaylist.SelectedItem :?> PlaylistItem
                this.PlayPlaylistIndex(item.Id)
        )

        this.Opened.Add(fun _ ->
            StartupLog.writef "MainWindow.Opened fired; IsVisible=%b WindowState=%A" this.IsVisible this.WindowState
            StartupLog.write "MainWindow initializing mpv observers"
            this.InitMpvObservers()
            StartupLog.write "MainWindow mpv observers initialized"
            StartupLog.write "MainWindow applying startup options"
            this.ApplyStartupOptions()
            StartupLog.write "MainWindow startup options applied"

            if startupArgs.HelpRequested then
                StartupLog.write "Help requested"
                txtStatus.Text <- "Usage: ImPlay.Fs [options] [url|path/]filename"
            else
                startupArgs.PlaylistFiles
                |> List.iter (fun path ->
                    StartupLog.writef "Loading startup playlist: %s" path
                    this.LoadPlaylistFile(path))

                if not startupArgs.Paths.IsEmpty then
                    StartupLog.writef "Loading startup paths: %A" startupArgs.Paths
                    this.LoadPaths(startupArgs.Paths, not startupArgs.PlaylistFiles.IsEmpty)

                startupArgs.SubtitleFiles
                |> List.iter (fun path ->
                    StartupLog.writef "Loading startup subtitle: %s" path
                    this.AddSubtitle(path, true))

            StartupLog.writef "MainWindow.Opened completed; IsVisible=%b Bounds=%A" this.IsVisible this.Bounds
        )

        this.Closing.Add(fun _ ->
            this.SaveWindowState()
        )

    member private this.GetPropertyInt(name: string) =
        if mpvView = null || mpvView.MpvContext = null then 0
        else
            match mpvView.MpvContext.GetPropertyString(name) with
            | null | "" -> 0
            | s -> 
                match Int32.TryParse(s) with
                | (true, v) -> v
                | _ -> 0

    member private this.GetPropertyDouble(name: string) =
        if mpvView = null || mpvView.MpvContext = null then 0.0
        else
            match mpvView.MpvContext.GetPropertyString(name) with
            | null | "" -> 0.0
            | s -> 
                match Double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture) with
                | (true, v) -> v
                | _ -> 0.0

    member private this.GetPropertyBool(name: string) =
        if mpvView = null || mpvView.MpvContext = null then false
        else
            match mpvView.MpvContext.GetPropertyString(name) with
            | "yes" | "true" | "1" -> true
            | _ -> false

    member private this.ShowNotification(message: string, ?durationMs: int) =
        let dur = defaultArg durationMs 2000
        txtNotification.Text <- message
        overlayNotification.IsVisible <- true
        task {
            do! Task.Delay(dur)
            Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                if txtNotification.Text = message then
                    overlayNotification.IsVisible <- false
            )
        } |> ignore

    member private this.HandleKey(e: KeyEventArgs) =
        if mpvView = null || mpvView.MpvContext = null then ()
        else
            match e.Key with
            | Key.Space | Key.P -> 
                isPaused <- not isPaused
                this.TryMpvSetBoolProperty("pause", isPaused) |> ignore
                this.ShowNotification(if isPaused then "Paused" else "Resume")
            | Key.F | Key.Enter ->
                btnFullscreen.RaiseEvent(RoutedEventArgs(Button.ClickEvent))
                this.ShowNotification(if isFullscreen then "Fullscreen On" else "Fullscreen Off")
            | Key.M ->
                isMuted <- not isMuted
                this.TryMpvSetBoolProperty("mute", isMuted) |> ignore
                this.ShowNotification(if isMuted then "Mute" else "Unmute")
            | Key.S ->
                if e.KeyModifiers = KeyModifiers.Control then
                    if this.TryMpvCommand([| "screenshot"; "window" |], "screenshot window") then
                        this.ShowNotification("Screenshot (Window)")
                else
                    if this.TryMpvCommand([| "screenshot" |], "screenshot") then
                        this.ShowNotification("Screenshot")
            | Key.Left -> 
                this.SeekRelative(-5.0)
                this.ShowNotification("Seek -5s")
            | Key.Right -> 
                this.SeekRelative(5.0)
                this.ShowNotification("Seek +5s")
            | Key.Up -> 
                this.SeekRelative(60.0)
                this.ShowNotification("Seek +1m")
            | Key.Down -> 
                this.SeekRelative(-60.0)
                this.ShowNotification("Seek -1m")
            | Key.OemPeriod ->
                if this.TryMpvCommand([| "frame-step" |], "frame step") then
                    this.ShowNotification("Frame Step")
            | Key.OemComma ->
                if this.TryMpvCommand([| "frame-back-step" |], "frame back") then
                    this.ShowNotification("Frame Back")
            | Key.A ->
                if this.TryMpvCommand([| "ab-loop" |], "ab loop a") then
                    this.ShowNotification("A-B Loop Set A")
            | Key.B ->
                if this.TryMpvCommand([| "ab-loop" |], "ab loop b") then
                    this.ShowNotification("A-B Loop Set B / Play")
            | Key.OemOpenBrackets -> 
                if this.TryMpvCommand([| "add"; "speed"; "-0.1" |], "speed decrease") then
                    this.ShowNotification(sprintf "Speed: %.2fx" (this.GetPropertyDouble("speed")))
            | Key.OemCloseBrackets -> 
                if this.TryMpvCommand([| "add"; "speed"; "0.1" |], "speed increase") then
                    this.ShowNotification(sprintf "Speed: %.2fx" (this.GetPropertyDouble("speed")))
            | Key.Back | Key.OemPipe ->
                if this.TryMpvCommand([| "set"; "speed"; "1.0" |], "speed reset") then
                    this.ShowNotification("Speed Reset (1.0x)")
            | Key.D9 | Key.OemMinus ->
                let v = Math.Max(0.0, sliderVolume.Value - 2.0)
                this.TryMpvSetProperty("volume", v.ToString("0.###", CultureInfo.InvariantCulture)) |> ignore
                this.ShowNotification(sprintf "Volume: %d%%" (int v))
            | Key.D0 | Key.OemPlus ->
                let v = Math.Min(100.0, sliderVolume.Value + 2.0)
                this.TryMpvSetProperty("volume", v.ToString("0.###", CultureInfo.InvariantCulture)) |> ignore
                this.ShowNotification(sprintf "Volume: %d%%" (int v))
            | Key.L ->
                if e.KeyModifiers = KeyModifiers.Control then
                    this.TogglePlaylistLoop()
                else
                    btnToggleSidebar.RaiseEvent(RoutedEventArgs(Button.ClickEvent))
            | Key.V ->
                if e.KeyModifiers = KeyModifiers.Control then
                    this.OpenFromClipboard() |> ignore
                    this.ShowNotification("Opening from clipboard...")
            | Key.PageUp ->
                if e.KeyModifiers = KeyModifiers.Shift then
                    if this.TryMpvCommand([| "playlist-prev" |], "playlist previous") then
                        this.ShowNotification("Previous Media")
                else
                    if this.TryMpvCommand([| "add"; "chapter"; "-1" |], "chapter previous") then
                        this.ShowNotification("Previous Chapter")
            | Key.PageDown ->
                if e.KeyModifiers = KeyModifiers.Shift then
                    if this.TryMpvCommand([| "playlist-next" |], "playlist next") then
                        this.ShowNotification("Next Media")
                else
                    if this.TryMpvCommand([| "add"; "chapter"; "1" |], "chapter next") then
                        this.ShowNotification("Next Chapter")
            | Key.Escape ->
                if isFullscreen then btnFullscreen.RaiseEvent(RoutedEventArgs(Button.ClickEvent))
                this.HidePalette()
            | _ -> ()

    member private this.ShowPalette(provider: string) =
        paletteItems.Clear()
        if mpvView <> null && mpvView.MpvContext <> null then
            let ctx = mpvView.MpvContext
            match provider with
            | "bindings" ->
                // Since LibMpv doesn't expose raw bindings easily in the wrapper, 
                // we'll add some common app commands for now.
                paletteItems.Add({ Title = "Open Files"; Tooltip = "Open media files"; Label = "Ctrl+O"; Id = 0; Action = fun () -> this.OpenFilesAsync() |> ignore })
                paletteItems.Add({ Title = "Open Folder"; Tooltip = "Open a directory"; Label = "Ctrl+F"; Id = 1; Action = fun () -> this.OpenFolderAsync() |> ignore })
                paletteItems.Add({ Title = "Open from Clipboard"; Tooltip = "Open path from clipboard"; Label = "Ctrl+V"; Id = 101; Action = fun () -> this.OpenFromClipboard() |> ignore })
                paletteItems.Add({ Title = "Screenshot"; Tooltip = "Take a screenshot"; Label = "S"; Id = 2; Action = fun () -> this.TryMpvCommand([| "async"; "screenshot" |], "palette screenshot") |> ignore })
                paletteItems.Add({ Title = "Toggle Fullscreen"; Tooltip = "Switch fullscreen mode"; Label = "Enter"; Id = 3; Action = fun () -> btnFullscreen.RaiseEvent(RoutedEventArgs(Button.ClickEvent)) })
                paletteItems.Add({ Title = "Settings"; Tooltip = "Open settings dialog"; Label = ""; Id = 4; Action = fun () -> menuSettings.RaiseEvent(RoutedEventArgs(MenuItem.ClickEvent)) })
                paletteItems.Add({ Title = "About"; Tooltip = "Show app information"; Label = ""; Id = 5; Action = fun () -> menuAbout.RaiseEvent(RoutedEventArgs(MenuItem.ClickEvent)) })
            | "chapters" ->
                let count = this.GetPropertyInt("chapters")
                for i in 0 .. count - 1 do
                    let title = ctx.GetPropertyString(sprintf "chapters/%d/title" i)
                    let time = this.GetPropertyDouble(sprintf "chapters/%d/time" i)
                    let displayTitle = if String.IsNullOrWhiteSpace(title) then sprintf "Chapter %d" (i+1) else title
                    paletteItems.Add({ Title = displayTitle; Tooltip = sprintf "Seek to %.1f" time; Label = "CHAPTER"; Id = i; Action = fun () -> this.TryMpvCommand([| "set"; "chapter"; string i |], "palette chapter") |> ignore })
            | "playlist" ->
                for i in 0 .. playlistPaths.Length - 1 do
                    let path = playlistPaths.[i]
                    paletteItems.Add({ Title = Media.titleFromPath path; Tooltip = path; Label = "PLAYLIST"; Id = i; Action = fun () -> this.PlayPlaylistIndex(i) })
            | "history" ->
                currentConfig.RecentFiles |> List.iteri (fun i f ->
                    paletteItems.Add({ Title = f.Title; Tooltip = f.Path; Label = "RECENT"; Id = i; Action = fun () -> this.LoadPaths([ f.Path ], false) })
                )
                if not currentConfig.RecentFiles.IsEmpty then
                    paletteItems.Add({ Title = "Clear History"; Tooltip = "Remove all recent files from history"; Label = "ACTION"; Id = -1; Action = fun () -> 
                        currentConfig <- { currentConfig with RecentFiles = [] }
                        Config.saveConfig currentConfig
                        recentFilesSource.Clear()
                        this.ShowNotification("History cleared")
                    })
            | _ -> ()

        txtPaletteInput.Text <- ""
        this.FilterPalette("")
        overlayPalette.IsVisible <- true
        txtPaletteInput.Focus() |> ignore

    member private this.HidePalette() =
        overlayPalette.IsVisible <- false

    member private this.FilterPalette(text: string) =
        paletteMatchesSource.Clear()
        let filtered = 
            if String.IsNullOrWhiteSpace(text) then
                paletteItems |> Seq.toList
            else
                let t = text.ToLower()
                paletteItems 
                |> Seq.filter (fun item -> item.Title.ToLower().Contains(t) || item.Tooltip.ToLower().Contains(t))
                |> Seq.toList
        
        paletteMatchesSource.AddRange(filtered :> IEnumerable<CommandItem>)
        if paletteMatchesSource.Count > 0 then
            listPaletteMatches.SelectedIndex <- 0

    member private this.ExecutePaletteItem(item: CommandItem) =
        this.HidePalette()
        item.Action()

    member private this.InitMpvObservers() =
        StartupLog.write "InitMpvObservers entered"
        if mpvView <> null && mpvView.MpvContext <> null then
            let ctx = mpvView.MpvContext
            StartupLog.write "InitMpvObservers found mpv context"
            
            ctx.Pause.Changed.Add(fun args ->
                Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                    if args.NewValue.HasValue then
                        isPaused <- args.NewValue.Value
                        if iconPlayPause <> null then
                            iconPlayPause.Data <- if isPaused then this.FindResource("IconPlay") :?> StreamGeometry else this.FindResource("IconPause") :?> StreamGeometry
                        if menuIconPlay <> null then
                            menuIconPlay.Data <- if isPaused then this.FindResource("IconPlay") :?> StreamGeometry else this.FindResource("IconPause") :?> StreamGeometry
                        if not isPaused then txtStatus.Text <- "Playing"
                        
                        // Update Taskbar state
                        let state = if isPaused then Platform.TbpFlag.Paused else Platform.TbpFlag.Normal
                        Platform.setTaskbarProgress this (if duration > 0.0 then sliderSeek.Value / 100.0 else 0.0) state
                )
            )

            ctx.Mute.Changed.Add(fun args ->
                Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                    if args.NewValue.HasValue then
                        isMuted <- args.NewValue.Value
                        if iconMuteUnmute <> null then
                            iconMuteUnmute.Data <- if isMuted then this.FindResource("IconMute") :?> StreamGeometry else this.FindResource("IconVolume") :?> StreamGeometry
                )
            )

            ctx.Volume.Changed.Add(fun args ->
                Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                    if args.NewValue.HasValue then
                        sliderVolume.Value <- args.NewValue.Value
                )
            )

            ctx.TimePos.Changed.Add(fun args ->
                Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                    if args.NewValue.HasValue && not isSeeking then
                        let nextValue =
                            if duration > 0.0 then
                                (args.NewValue.Value / duration * 100.0) |> max 0.0 |> min 100.0
                            else
                                this.GetPropertyDouble("percent-pos") |> max 0.0 |> min 100.0
                        this.UpdateSeekFromPercent(nextValue)
                        // Update Taskbar progress
                        if duration > 0.0 || nextValue > 0.0 then
                            let state = if isPaused then Platform.TbpFlag.Paused else Platform.TbpFlag.Normal
                            Platform.setTaskbarProgress this (nextValue / 100.0) state
                )
            )

            ctx.Duration.Changed.Add(fun args ->
                Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                    if args.NewValue.HasValue then
                        let nextDuration = args.NewValue.Value
                        if Double.IsNaN(nextDuration) || Double.IsInfinity(nextDuration) || nextDuration <= 0.0 then
                            duration <- 0.0
                            sliderSeek.Maximum <- 100.0
                            let percent = this.GetPropertyDouble("percent-pos")
                            if percent > 0.0 then
                                this.UpdateSeekFromPercent(percent)
                        else
                            duration <- nextDuration
                            sliderSeek.Maximum <- 100.0
                            sliderSeek.Value <- sliderSeek.Value |> max 0.0 |> min 100.0
                            sliderSeek.IsEnabled <- true
                        this.UpdateTimecodes()
                )
            )

            ctx.Fullscreen.Changed.Add(fun args ->
                Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                    if args.NewValue.HasValue then
                        isFullscreen <- args.NewValue.Value
                        this.WindowState <- if isFullscreen then WindowState.FullScreen else WindowState.Normal
                )
            )

            ctx.PlaylistPosition.Changed.Add(fun args ->
                Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                    if args.NewValue.HasValue then
                        currentIndex <- int args.NewValue.Value
                        this.RefreshPlaylistFromMpv()
                )
            )

            ctx.PropertyChanged.Add(fun args ->
                match args.Name with
                | "chapter-list" | "chapter" -> 
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () -> this.RefreshChapters())
                | "track-list" | "vid" | "aid" | "sid" -> 
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () -> this.RefreshTracks())
                | "playlist" | "playlist-count" ->
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () -> this.RefreshPlaylistFromMpv())
                | "percent-pos" ->
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                        this.UpdateSeekFromPercent(this.GetPropertyDouble("percent-pos"))
                    )
                | "ab-loop-a" | "ab-loop-b" ->
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                        let a = ctx.GetPropertyString("ab-loop-a")
                        let b = ctx.GetPropertyString("ab-loop-b")
                        if btnLoopA <> null then 
                            btnLoopA.Foreground <- if a = "no" then Brushes.White else Brushes.LightGreen
                        if btnLoopB <> null then 
                            btnLoopB.Foreground <- if b = "no" then Brushes.White else Brushes.LightGreen
                    )
                | "glsl-shaders" ->
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () -> this.RefreshShaders())
                | _ -> ()
            )

            // Track file loading for status text
            ctx.FileLoaded.Add(fun _ ->
                Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                    StartupLog.write "mpv FileLoaded event received"
                    duration <- 0.0
                    sliderSeek.Maximum <- 100.0
                    sliderSeek.Value <- 0.0
                    sliderSeek.IsEnabled <- true
                    this.UpdateTimecodes()
                    let title = ctx.GetPropertyString("media-title")
                    txtStatus.Text <- sprintf "Playing: %s" title
                    let path = ctx.GetPropertyString("path")
                    if not (String.IsNullOrEmpty(path)) && path <> "bd://" && path <> "dvd://" then
                        this.AddRecentFile(path, title)
                    
                    // Reset taskbar progress
                    Platform.setTaskbarProgress this 0.0 Platform.TbpFlag.Normal
                )
            )
        else
            StartupLog.writef "InitMpvObservers skipped; mpvView null=%b context null=%b" (mpvView = null) (mpvView <> null && mpvView.MpvContext = null)

    member private this.OpenFilesAsync() =
        task {
            if this.StorageProvider.CanOpen then
                let options = FilePickerOpenOptions()
                options.Title <- "Open media"
                options.AllowMultiple <- true
                options.FileTypeFilter <- 
                    Media.mediaFilters @ [ Media.subtitleFilter; Media.isoFilter ]

                let! files = this.StorageProvider.OpenFilePickerAsync(options)
                files
                |> Seq.map this.StorageItemToPath
                |> Seq.filter (String.IsNullOrWhiteSpace >> not)
                |> Seq.toList
                |> fun paths -> this.LoadPaths(paths, false)
            else
                txtStatus.Text <- "Open dialog is not available on this platform"
        } :> Task

    member private this.OpenFolderAsync() =
        task {
            if this.StorageProvider.CanPickFolder then
                let options = FolderPickerOpenOptions()
                options.Title <- "Open folder"
                options.AllowMultiple <- false

                let! folders = this.StorageProvider.OpenFolderPickerAsync(options)
                folders
                |> Seq.map this.StorageItemToPath
                |> Seq.tryHead
                |> Option.iter (fun path ->
                    let files =
                        try
                            Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                            |> Seq.filter Media.isMediaFile
                            |> fun items -> Media.naturalSort items id
                        with ex ->
                            txtStatus.Text <- sprintf "Error: %s" ex.Message
                            []

                    this.LoadPaths(files, false)
                )
            else
                txtStatus.Text <- "Folder picker is not available on this platform"
        } :> Task

    member private this.GetDroppedPaths(args: DragEventArgs) =
        let fromTransfer =
            if args.DataTransfer <> null then
                args.DataTransfer.Items
                |> Seq.choose (fun item ->
                    let raw = item.TryGetRaw(DataFormat.File)
                    match raw with
                    | :? IStorageItem as storageItem -> Some(this.StorageItemToPath(storageItem))
                    | :? seq<IStorageItem> as storageItems ->
                        storageItems |> Seq.map this.StorageItemToPath |> String.concat "\n" |> Some
                    | _ -> None
                )
                |> Seq.collect (fun item -> item.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                |> Seq.toList
            else
                []

        fromTransfer

    member private this.StorageItemToPath(item: IStorageItem) =
        if item.Path <> null && item.Path.IsFile then
            item.Path.LocalPath
        elif item.Path <> null then
            item.Path.LocalPath
        else
            item.Name

    member private this.ExpandInputPaths(paths: string list) =
        paths
        |> List.collect (fun path ->
            if Directory.Exists(path) then
                if Media.isDiscDirectory path then
                    [ path ]
                else
                try
                    Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    |> Seq.filter Media.isMediaFile
                    |> fun items -> Media.naturalSort items id
                with ex ->
                    txtStatus.Text <- sprintf "Error: %s" ex.Message
                    []
            else
                [ path ]
        )

    member private this.LoadPaths(paths: string list, append: bool) =
        if mpvView <> null && mpvView.MpvContext <> null then
            try
                let playable =
                    this.ExpandInputPaths(paths)
                    |> List.filter (fun path ->
                        not (String.IsNullOrWhiteSpace(path)) &&
                        (Directory.Exists(path) || File.Exists(path) || Uri.IsWellFormedUriString(path, UriKind.Absolute))
                    )

                if playable.IsEmpty then
                    txtStatus.Text <- "No playable media found"
                else
                    if not append then
                        this.ClearPlaylist()

                    let mutable firstLoaded: string option = None

                    playable
                    |> List.iteri (fun index path ->
                        if Media.isSubtitleFile path then
                            this.AddSubtitle(path, append || index > 0)
                        elif Media.isIsoFile path then
                            this.LoadDisc(path, Media.discKindForIso path, firstLoaded.IsNone)
                            if firstLoaded.IsNone then
                                firstLoaded <- Some path
                        elif Directory.Exists(path) && Media.isDiscDirectory path then
                            this.LoadDisc(path, Media.discKindForDirectory path, firstLoaded.IsNone)
                            if firstLoaded.IsNone then
                                firstLoaded <- Some path
                        else
                            let appendFile = append || index > 0
                            let appendPlay = index > 0
                            mpvView.MpvContext.LoadFile(path, appendFile, appendPlay, null).Invoke()
                            // this.AddPlaylistItem(path, firstLoaded.IsNone) // Removed: sync via observer
                            this.AddRecentFile(path, Media.titleFromPath path)
                            if firstLoaded.IsNone then
                                currentPath <- Some path
                                firstLoaded <- Some path
                    )

                    match firstLoaded with
                    | Some path ->
                        isPaused <- false
                        if iconPlayPause <> null then iconPlayPause.Data <- this.FindResource("IconPause") :?> StreamGeometry
                        txtStatus.Text <- sprintf "Playing: %s" (Media.titleFromPath path)
                    | None -> ()
            with ex ->
                txtStatus.Text <- sprintf "Error: %s" ex.Message

    member private this.LoadDisc(path: string, kind: Media.DiscKind, playing: bool) =
        let propertyName, loadPath =
            match kind with
            | Media.Bluray -> "bluray-device", "bd://"
            | Media.Dvd -> "dvd-device", "dvd://"

        this.TryMpvCommand([| "set"; propertyName; path |], "disc device") |> ignore
        mpvView.MpvContext.LoadFile(loadPath, not playing, false, null).Invoke()
        // this.AddPlaylistItem(path, playing) // Removed: sync via observer
        this.AddRecentFile(path, Media.titleFromPath path)
        if playing then
            currentPath <- Some path

    member private this.ApplyStartupOptions() =
        if mpvView <> null && mpvView.MpvContext <> null then
            let ctx = mpvView.MpvContext
            
            // Load bundled config
            let bundledDir = Platform.getBundledConfigPath()
            let mpvConf = Path.Combine(bundledDir, "mpv.conf")
            let inputConf = Path.Combine(bundledDir, "input.conf")
            
            if File.Exists(mpvConf) then
                this.TryMpvCommand([| "load-config"; mpvConf |], "load bundled mpv.conf") |> ignore
            if File.Exists(inputConf) then
                this.TryMpvSetProperty("input-conf", inputConf) |> ignore
            
            // osc.lua is handled by mpv if it's in the scripts directory, 
            // but we might need to load it manually if it's bundled differently.
            let oscLua = Path.Combine(bundledDir, "osc.lua")
            if File.Exists(oscLua) then
                this.TryMpvCommand([| "load-script"; oscLua |], "load bundled osc.lua") |> ignore

            if currentConfig.Window.Single then
                this.TryMpvSetProperty("input-ipc-server", Ipc.getIpcPipeName()) |> ignore

            startupArgs.Options
            |> List.iter (fun (key, value) ->
                if not (this.TryMpvSetProperty(key, value, sprintf "option --%s" key)) then
                    txtStatus.Text <- sprintf "Option ignored: --%s=%s" key value
            )

    member private this.LoadPlaylistFile(path: string) =
        if mpvView <> null && mpvView.MpvContext <> null then
            try
                mpvView.MpvContext.LoadPlaylist(path, true).Invoke()
                // this.AddPlaylistItem(path, playlistPaths.IsEmpty) // Removed: sync via observer
                txtStatus.Text <- sprintf "Loaded playlist: %s" (Media.titleFromPath path)
            with ex ->
                txtStatus.Text <- sprintf "Error: %s" ex.Message

    member private this.AddSubtitle(path: string, append: bool) =
        if mpvView <> null && mpvView.MpvContext <> null then
            try
                let mode = if append then "auto" else "select"
                this.TryMpvCommand([| "sub-add"; path; mode |], "subtitle add") |> ignore
            with ex ->
                txtStatus.Text <- sprintf "Error: %s" ex.Message

    member private this.PlayPlaylistIndex(index: int) =
        if mpvView <> null && mpvView.MpvContext <> null && index >= 0 && index < playlistPaths.Length then
            try
                this.TryMpvCommand([| "playlist-play-index"; string index |], "playlist play index") |> ignore
                currentIndex <- index
                currentPath <- Some playlistPaths.[index]
                isPaused <- false
                if iconPlayPause <> null then iconPlayPause.Data <- this.FindResource("IconPause") :?> StreamGeometry
                txtStatus.Text <- sprintf "Playing: %s" (Media.titleFromPath playlistPaths.[index])
                this.RefreshPlaylistFromMpv()
            with ex ->
                txtStatus.Text <- sprintf "Error: %s" ex.Message

    member private this.PlaylistSort(reverse: bool) =
        if mpvView <> null && mpvView.MpvContext <> null && not playlistPaths.IsEmpty then
            try
                let ctx = mpvView.MpvContext
                let items = 
                    [ 0 .. playlistPaths.Length - 1 ]
                    |> List.map (fun i ->
                        let path = playlistPaths.[i]
                        let title = playlistSource.[i].Title
                        {| Id = i; Path = path; Title = title |}
                    )
                
                let sorted = 
                    items 
                    |> List.sortWith (fun a b -> 
                        let str1 = if String.IsNullOrWhiteSpace(a.Title) then a.Path else a.Title
                        let str2 = if String.IsNullOrWhiteSpace(b.Title) then b.Path else b.Title
                        Media.naturalCompare str1 str2
                    )
                
                let final = if reverse then List.rev sorted else sorted
                
                // Find current playing position in new list
                let newPos = final |> List.findIndex (fun x -> x.Id = currentIndex)
                
                // Build M3U list for memory loading
                let sb = System.Text.StringBuilder()
                sb.AppendLine("#EXTM3U") |> ignore
                for item in final do
                    if not (String.IsNullOrWhiteSpace(item.Title)) then
                        sb.AppendLine(sprintf "#EXTINF:-1,%s" item.Title) |> ignore
                    sb.AppendLine(item.Path) |> ignore
                
                let timePos = this.GetPropertyDouble("time-pos")
                
                // Set start position and time before loading
                this.TryMpvSetProperty("playlist-start", string newPos) |> ignore
                this.TryMpvSetProperty("start", sprintf "+%s" (timePos.ToString("0.###", CultureInfo.InvariantCulture))) |> ignore
                
                if not (this.GetPropertyBool("idle-active")) then
                    this.TryMpvCommand([| "playlist-clear" |], "playlist clear before sort") |> ignore
                
                this.TryMpvCommand([| "loadlist"; "memory://" + sb.ToString(); if this.GetPropertyBool("idle-active") then "append" else "replace" |], "playlist sort loadlist") |> ignore
                this.ShowNotification(sprintf "Playlist sorted (%s)" (if reverse then "desc" else "asc"))
            with ex ->
                txtStatus.Text <- sprintf "Error sorting playlist: %s" ex.Message

    member private this.ClearPlaylist() =
        if mpvView <> null && mpvView.MpvContext <> null then
            try
                mpvView.MpvContext.PlaylistClear().Invoke()
            with _ ->
                ()
        playlistPaths <- []
        playlistSource.Clear()
        currentIndex <- -1

    member private this.SavePlaylistAsync() =
        task {
            if this.StorageProvider.CanSave then
                let options = FilePickerSaveOptions()
                options.Title <- "Save Playlist"
                options.DefaultExtension <- "m3u"
                options.FileTypeChoices <- [ FilePickerFileType("M3U Playlist", Patterns = [ "*.m3u"; "*.m3u8" ]) ]
                
                let! file = this.StorageProvider.SaveFilePickerAsync(options)
                match file with
                | null -> ()
                | f ->
                    let path = this.StorageItemToPath f
                    if not (String.IsNullOrWhiteSpace path) then
                        try
                            let sb = System.Text.StringBuilder()
                            sb.AppendLine("#EXTM3U") |> ignore
                            for item in playlistSource do
                                if not (String.IsNullOrWhiteSpace item.Title) then
                                    sb.AppendLine(sprintf "#EXTINF:-1,%s" item.Title) |> ignore
                                sb.AppendLine(item.Path) |> ignore
                            
                            File.WriteAllText(path, sb.ToString())
                            this.ShowNotification("Playlist saved")
                        with ex ->
                            txtStatus.Text <- sprintf "Error saving playlist: %s" ex.Message
        } :> Task

    member private this.RefreshPlaylistFromMpv() =
        if mpvView <> null && mpvView.MpvContext <> null then
            let ctx = mpvView.MpvContext
            try
                let count = this.GetPropertyInt("playlist-count")
                let current = int (this.GetPropertyDouble("playlist-playing-pos"))
                
                let newList = 
                    [ 0 .. count - 1 ]
                    |> List.map (fun i ->
                        let path = ctx.GetPropertyString(sprintf "playlist/%d/filename" i)
                        let title = ctx.GetPropertyString(sprintf "playlist/%d/title" i)
                        let displayTitle = if String.IsNullOrWhiteSpace(title) then Media.titleFromPath path else title
                        { Id = i; Path = path; Title = displayTitle; IsPlaying = i = current }
                    )
                
                playlistSource.Clear()
                playlistSource.AddRange(newList)
                playlistPaths <- newList |> List.map (fun x -> x.Path)
                currentIndex <- current
            with _ -> ()

    member private this.RefreshChapters() =
        if mpvView <> null && mpvView.MpvContext <> null then
            let ctx = mpvView.MpvContext
            chaptersSource.Clear()
            try
                let count = this.GetPropertyInt("chapters")
                for i in 0 .. count - 1 do
                    let title = ctx.GetPropertyString(sprintf "chapters/%d/title" i)
                    let time = this.GetPropertyDouble(sprintf "chapters/%d/time" i)
                    chaptersSource.Add({ Id = i; Title = (if String.IsNullOrWhiteSpace(title) then sprintf "Chapter %d" (i + 1) else title); Time = time })
            with _ -> ()

    member private this.RefreshTracks() =
        if mpvView <> null && mpvView.MpvContext <> null then
            let ctx = mpvView.MpvContext
            videoTracksSource.Clear()
            audioTracksSource.Clear()
            subTracksSource.Clear()
            
            try
                let count = this.GetPropertyInt("track-list/count")
                for i in 0 .. count - 1 do
                    let tType = ctx.GetPropertyString(sprintf "track-list/%d/type" i)
                    let id = this.GetPropertyInt(sprintf "track-list/%d/id" i)
                    let title = ctx.GetPropertyString(sprintf "track-list/%d/title" i)
                    let lang = ctx.GetPropertyString(sprintf "track-list/%d/lang" i)
                    let codec = ctx.GetPropertyString(sprintf "track-list/%d/codec" i)
                    let selected = this.GetPropertyBool(sprintf "track-list/%d/selected" i)
                    
                    let details =
                        match tType with
                        | "video" ->
                            let w = this.GetPropertyInt(sprintf "track-list/%d/demux-w" i)
                            let h = this.GetPropertyInt(sprintf "track-list/%d/demux-h" i)
                            if w > 0 && h > 0 then sprintf "%dx%d" w h else ""
                        | "audio" ->
                            let rate = this.GetPropertyInt(sprintf "track-list/%d/demux-samplerate" i)
                            let chans = ctx.GetPropertyString(sprintf "track-list/%d/demux-channels" i)
                            if rate > 0 then sprintf "%dHz %s" rate chans else chans
                        | _ -> ""

                    let displayTitle = 
                        if not (String.IsNullOrWhiteSpace(title)) then title
                        elif not (String.IsNullOrWhiteSpace(lang)) then sprintf "Track %d [%s]" id lang
                        else sprintf "Track %d" id

                    let secondaryId = this.GetPropertyInt("secondary-sid")
                    let isSecondary = tType = "sub" && id = secondaryId

                    let item = { Id = id; Type = tType; Title = displayTitle; Lang = lang; Codec = codec; Details = details; Selected = selected; IsSecondary = isSecondary }
                    match tType with
                    | "video" -> videoTracksSource.Add(item)
                    | "audio" -> audioTracksSource.Add(item)
                    | "sub" -> subTracksSource.Add(item)
                    | _ -> ()
            with _ -> ()

    member private this.RefreshAudioDevices() =
        if mpvView <> null && mpvView.MpvContext <> null then
            let ctx = mpvView.MpvContext
            try
                // Use a simpler way if possible, or parse the node list.
                // For now, let's just get the count and loop.
                let count = this.GetPropertyInt("audio-device-list/count")
                let mutable devices = []
                for i in 0 .. count - 1 do
                    let name = ctx.GetPropertyString(sprintf "audio-device-list/%d/name" i)
                    let desc = ctx.GetPropertyString(sprintf "audio-device-list/%d/description" i)
                    devices <- { Name = name; Description = desc } :: devices
                
                playerState <- { playerState with AudioDevices = List.rev devices }
            with _ -> ()

    member private this.AddRecentFile(path: string, title: string) =
        let existing = currentConfig.RecentFiles |> List.tryFind (fun f -> f.Path = path)
        let newList = 
            match existing with
            | Some item -> item :: (currentConfig.RecentFiles |> List.filter (fun f -> f.Path <> path))
            | None -> { Path = path; Title = title } :: currentConfig.RecentFiles
            |> List.truncate currentConfig.Recent.Limit
                
        currentConfig <- { currentConfig with RecentFiles = newList }
        Config.saveConfig currentConfig
                
        recentFilesSource.Clear()
        recentFilesSource.AddRange(newList)

    member private this.PopulateDynamicMenus() =
        this.UpdateMenuStates()
        this.RefreshTracks()
        this.RefreshAudioDevices()
        this.RefreshShaders()
        this.PopulateRecentMenu()
        this.PopulatePlaylistMenu()
        this.PopulateChaptersMenu()
        this.PopulateTracksMenu("audio", "aid", menuAudioTracks)
        this.PopulateTracksMenu("video", "vid", menuVideoTracks)
        this.PopulateTracksMenu("sub", "sid", menuSubTracks)
        this.PopulateTracksMenu("sub", "secondary-sid", menuSubSecondary)
        this.PopulateAudioDevicesMenu()
        this.PopulateProfilesMenu()
        this.PopulateThemesMenu()

    member private this.UpdateMenuStates() =
        if mpvView = null || mpvView.MpvContext = null then ()
        else
            try
                let ctx = mpvView.MpvContext
                // Get current state
                let isPausedVal = ctx.Pause.Get().GetValueOrDefault()
                let isPlaying = not isPausedVal
                let playlistCount = playlistPaths.Length
                let chapterCount = chaptersSource.Count
                
                // Update Play/Pause menu
                menuPlay.Header <- if isPausedVal then Lang.i18n "menu.play" else Lang.i18n "menu.pause"
                
                // Update enabled states
                menuStop.IsEnabled <- isPlaying
                menuSeekForward10.IsEnabled <- isPlaying
                menuSeekForward60.IsEnabled <- isPlaying
                menuSeekBack10.IsEnabled <- isPlaying
                menuSeekBack60.IsEnabled <- isPlaying
                menuFrameStep.IsEnabled <- isPlaying
                menuFrameBack.IsEnabled <- isPlaying
                menuPlaylistNext.IsEnabled <- playlistCount > 1
                menuPlaylistPrev.IsEnabled <- playlistCount > 1
                menuChapterNext.IsEnabled <- chapterCount > 1
                menuChapterPrev.IsEnabled <- chapterCount > 1
                menuFullscreen.IsEnabled <- isPlaying
                menuScreenshot.IsEnabled <- isPlaying
                
                // Update check marks for loop modes
                let loopFile = ctx.GetPropertyString("loop-file")
                let loopPlaylist = ctx.GetPropertyString("loop-playlist")
                menuFileLoop.IsChecked <- loopFile <> "no"
                menuPlaylistLoop.IsChecked <- loopPlaylist <> "no"

                // Update Hwdec and Deinterlace
                menuVideoHwdec.IsChecked <- ctx.GetPropertyString("hwdec") <> "no"
                menuVideoDeinterlace.IsChecked <- ctx.GetPropertyString("deinterlace") = "yes"
                
                // Update Window Options
                menuOnTop.IsChecked <- this.Topmost
                menuWindowBorder.IsChecked <- ctx.GetPropertyString("border") = "yes"
                menuWindowDragging.IsChecked <- ctx.GetPropertyString("window-dragging") = "yes"

                // Update current file actions
                let hasCurrent = currentPath.IsSome
                menuCopyPath.IsEnabled <- hasCurrent
                menuRevealCurrent.IsEnabled <- hasCurrent

                // Update Video Rotate
                let rotate = ctx.GetPropertyString("video-rotate")
                menuVideoRotate0.IsChecked <- (rotate = "0")
                menuVideoRotate90.IsChecked <- (rotate = "90")
                menuVideoRotate180.IsChecked <- (rotate = "180")
                menuVideoRotate270.IsChecked <- (rotate = "270")

                // Update Scale checkmarks
                let scale = this.GetPropertyDouble("window-scale")
                menuVideoScale200.IsChecked <- (Math.Abs(scale - 2.0) < 0.01)
                menuVideoScale150.IsChecked <- (Math.Abs(scale - 1.5) < 0.01)
                menuVideoScale100.IsChecked <- (Math.Abs(scale - 1.0) < 0.01)
                menuVideoScale75.IsChecked <- (Math.Abs(scale - 0.75) < 0.01)
                menuVideoScale50.IsChecked <- (Math.Abs(scale - 0.5) < 0.01)
                menuVideoScale25.IsChecked <- (Math.Abs(scale - 0.25) < 0.01)
                
                // Update Aspect checkmarks
                let aspect = ctx.GetPropertyString("video-aspect-override")
                menuVideoAspect16_9.IsChecked <- (aspect = "16:9")
                menuVideoAspect16_10.IsChecked <- (aspect = "16:10")
                menuVideoAspect4_3.IsChecked <- (aspect = "4:3")
                menuVideoAspect235.IsChecked <- (aspect = "2.35:1")
                menuVideoAspect185.IsChecked <- (aspect = "1.85:1")
                menuVideoAspect1.IsChecked <- (aspect = "1:1")
                menuVideoAspectReset.IsChecked <- (aspect = "-1" || aspect = "no")

                // Update Quality checkmarks (best effort based on ytdl-format)
                let qual = ctx.GetPropertyString("ytdl-format")
                menuVideoQual4320.IsChecked <- (qual.Contains("4320"))
                menuVideoQual2160.IsChecked <- (qual.Contains("2160"))
                menuVideoQual1440.IsChecked <- (qual.Contains("1440"))
                menuVideoQual1080.IsChecked <- (qual.Contains("1080"))
                menuVideoQual720.IsChecked <- (qual.Contains("720"))
                menuVideoQual480.IsChecked <- (qual.Contains("480"))

            with ex ->
                printfn "Error updating menu states: %s" ex.Message

    member private this.OpenUrl() = 
        task {
            let! url = this.ShowUrlInputDialog()
            match url with
            | Some url when not (String.IsNullOrWhiteSpace url) ->
                this.LoadPaths([url], false)
            | _ -> ()
        } :> Task

    member private this.ShowUrlInputDialog() = task {
        let dialog = Window(Title = "Open URL", Width = 450.0, Height = 160.0, WindowStartupLocation = WindowStartupLocation.CenterOwner)
        dialog.Background <- SolidColorBrush(Color.Parse("#1E1E1E"))
        dialog.CanResize <- false
        
        let stack = StackPanel(Margin = Thickness(20.0), Spacing = 15.0)
        let label = TextBlock(Text = "Enter media URL (YouTube, Twitch, direct link, etc.):", Foreground = Brushes.White)
        let input: TextBox = TextBox(Watermark = "https://...", UseFloatingWatermark = true)
        
        let buttons = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10.0)
        let btnOk = Button(Content = "Open", IsDefault = true, Width = 80.0)
        btnOk.Classes.Add("ControlBtn")
        let btnCancel = Button(Content = "Cancel", IsCancel = true, Width = 80.0)
        
        buttons.Children.Add(btnOk)
        buttons.Children.Add(btnCancel)
        stack.Children.Add(label)
        stack.Children.Add(input)
        stack.Children.Add(buttons)
        dialog.Content <- stack
        
        let mutable result = None
        btnOk.Click.Add(fun _ -> 
            result <- Some input.Text
            dialog.Close())
        btnCancel.Click.Add(fun _ -> dialog.Close())
        
        do! dialog.ShowDialog(this)
        return result
    }

    member private this.OpenDisk() = 
        task {
            if this.StorageProvider.CanPickFolder then
                let options = FolderPickerOpenOptions(Title = "Select DVD/Blu-ray Drive or Folder")
                let! folders = this.StorageProvider.OpenFolderPickerAsync(options)
                if not (Seq.isEmpty folders) then
                    let path = this.StorageItemToPath (Seq.head folders)
                    if not (String.IsNullOrWhiteSpace path) then
                        this.LoadPaths([ sprintf "bd://%s" path ], false)
        } :> Task

    member private this.OpenIso() = 
        task {
            if this.StorageProvider.CanOpen then
                let options = FilePickerOpenOptions(Title = "Open ISO File")
                options.FileTypeFilter <- [ FilePickerFileType("ISO Files", Patterns = [ "*.iso" ]) ]
                let! files = this.StorageProvider.OpenFilePickerAsync(options)
                if not (Seq.isEmpty files) then
                    let path = this.StorageItemToPath (Seq.head files)
                    if not (String.IsNullOrWhiteSpace path) then
                        this.LoadPaths([ sprintf "bd://%s" path ], false)
        } :> Task


    member private this.PopulateRecentMenu() =
        if menuRecent <> null then
            menuRecent.Items.Clear()
            currentConfig.RecentFiles
            |> List.truncate 10
            |> List.iter (fun file ->
                let item = MenuItem(Header = file.Title)
                item.Click.Add(fun _ -> this.LoadPaths([ file.Path ], false))
                menuRecent.Items.Add(item) |> ignore
                ()
            )
            if not currentConfig.RecentFiles.IsEmpty then
                menuRecent.Items.Add(Separator()) |> ignore
                let clearItem = MenuItem(Header = "Clear History")
                clearItem.Click.Add(fun _ ->
                    currentConfig <- { currentConfig with RecentFiles = [] }
                    Config.saveConfig currentConfig
                    recentFilesSource.Clear()
                    this.ShowNotification("History cleared")
                )
                menuRecent.Items.Add(clearItem) |> ignore
                ()

    member private this.PopulatePlaylistMenu() =
        if menuPlaylist <> null then
            menuPlaylist.Items.Clear()
            playlistPaths
            |> List.iteri (fun i path ->
                if i < 15 then
                    let title = Media.titleFromPath path
                    let item = MenuItem(Header = title, IsChecked = (i = currentIndex))
                    item.Click.Add(fun _ -> this.PlayPlaylistIndex(i))
                    menuPlaylist.Items.Add(item) |> ignore
            )
            if playlistPaths.Length > 15 then
                menuPlaylist.Items.Add(MenuItem(Header = sprintf "... and %d more" (playlistPaths.Length - 15), IsEnabled = false)) |> ignore
                ()
            
            if not playlistPaths.IsEmpty then
                menuPlaylist.Items.Add(Separator()) |> ignore
                let clearItem = MenuItem(Header = "Clear Playlist")
                clearItem.Click.Add(fun _ -> this.ClearPlaylist())
                menuPlaylist.Items.Add(clearItem) |> ignore

    member private this.PopulateChaptersMenu() =
        if menuChapters <> null && mpvView <> null && mpvView.MpvContext <> null then
            menuChapters.Items.Clear()
            // In a real app, we'd parse the chapter-list property here.
            // For the prototype, we'll check if there are chapters.
            let count = this.GetPropertyInt("chapters")
            if count > 0 then
                for i in 0 .. count - 1 do
                    if i < 20 then
                        let title = sprintf "Chapter %d" (i + 1)
                        let item = MenuItem(Header = title)
                        item.Click.Add(fun _ -> 
                            this.TryMpvCommand([| "set"; "chapter"; string i |], "chapter menu select") |> ignore
                        )
                        menuChapters.Items.Add(item) |> ignore
                        ()
            else
                menuChapters.Items.Add(MenuItem(Header = "No Chapters", IsEnabled = false)) |> ignore
                ()

    member private this.RevealPath(path: string) =
        if not (String.IsNullOrWhiteSpace path) then
            try
                if File.Exists(path) || Directory.Exists(path) then
                    Process.Start(ProcessStartInfo(FileName = "explorer.exe", Arguments = sprintf "/select,\"%s\"" path, UseShellExecute = true)) |> ignore
            with _ -> ()

    member private this.PopulateTracksMenu(trackType: string, propName: string, menu: MenuItem) =
        if menu <> null && mpvView <> null && mpvView.MpvContext <> null then
            menu.Items.Clear()
            let count = this.GetPropertyInt("track-list/count")
            let mutable found = false
            let currentId = if propName.Contains("sid") || propName.Contains("aid") || propName.Contains("vid") then mpvView.MpvContext.GetPropertyString(propName) else ""
            
            for i in 0 .. count - 1 do
                let tType = mpvView.MpvContext.GetPropertyString(sprintf "track-list/%d/type" i)
                if tType = trackType then
                    found <- true
                    let id = this.GetPropertyInt(sprintf "track-list/%d/id" i)
                    let title = mpvView.MpvContext.GetPropertyString(sprintf "track-list/%d/title" i)
                    let lang = mpvView.MpvContext.GetPropertyString(sprintf "track-list/%d/lang" i)
                    let isChecked = string id = currentId
                    
                    let displayTitle = 
                        if not (String.IsNullOrWhiteSpace(title)) then title
                        elif not (String.IsNullOrWhiteSpace(lang)) then sprintf "Track %d [%s]" id lang
                        else sprintf "Track %d" id
                    
                    let item = MenuItem(Header = displayTitle, IsChecked = isChecked)
                    item.Click.Add(fun _ ->
                        this.TryMpvCommand([| "set"; propName; string id |], sprintf "track select %s" propName) |> ignore
                    )
                    menu.Items.Add(item) |> ignore
                    ()
            
            if found then
                menu.Items.Add(Separator()) |> ignore
                let disableItem = MenuItem(Header = "Disable", IsChecked = (currentId = "no"))
                disableItem.Click.Add(fun _ ->
                    this.TryMpvCommand([| "set"; propName; "no" |], sprintf "track disable %s" propName) |> ignore
                )
                menu.Items.Add(disableItem) |> ignore
                ()
            else
                menu.Items.Add(MenuItem(Header = "None", IsEnabled = false)) |> ignore
                ()

    member private this.PopulateProfilesMenu() =
        if menuProfiles <> null && mpvView <> null && mpvView.MpvContext <> null then
            menuProfiles.Items.Clear()
            let ctx = mpvView.MpvContext
            try
                let count = this.GetPropertyInt("profile-list/count")
                if count > 0 then
                    for i in 0 .. count - 1 do
                        let name = ctx.GetPropertyString(sprintf "profile-list/%d/name" i)
                        if not (String.IsNullOrWhiteSpace(name)) then
                            let item = MenuItem(Header = name)
                            item.Click.Add(fun _ ->
                                if this.TryMpvCommand([| "apply-profile"; name |], "apply profile") then
                                    this.ShowNotification(sprintf "Profile applied: %s" name)
                            )
                            menuProfiles.Items.Add(item) |> ignore
                else
                    menuProfiles.Items.Add(MenuItem(Header = "No Profiles", IsEnabled = false)) |> ignore
                    ()
            with _ ->
                menuProfiles.Items.Add(MenuItem(Header = "No Profiles", IsEnabled = false)) |> ignore
                ()

    member private this.PopulateThemesMenu() =
        if menuThemes <> null then
            menuThemes.Items.Clear()
            let themes = [ "Dark"; "Light"; "System" ]
            themes |> List.iter (fun theme ->
                let item = MenuItem(Header = theme, IsChecked = (currentConfig.Interface.Theme.ToLower() = theme.ToLower()))
                item.Click.Add(fun _ ->
                    currentConfig <- { currentConfig with Interface = { currentConfig.Interface with Theme = theme.ToLower() } }
                    Config.saveConfig currentConfig
                    this.ApplyTheme(theme)
                    this.ShowNotification(sprintf "Theme set to %s" theme)
                )
                menuThemes.Items.Add(item) |> ignore
                ()
            )

    member private this.ApplyTheme(themeName: string) =
        let app = Application.Current
        if app <> null then
            match themeName.ToLower() with
            | "dark" -> app.RequestedThemeVariant <- ThemeVariant.Dark
            | "light" -> app.RequestedThemeVariant <- ThemeVariant.Light
            | _ -> app.RequestedThemeVariant <- ThemeVariant.Default

    member private this.OpenFromClipboard() =
        task {
            let clipboard = this.Clipboard
            if clipboard <> null then
                let! text = clipboard.GetTextAsync() // Reverting to GetTextAsync for now to fix build, will investigate extension method later if needed
                if not (String.IsNullOrWhiteSpace(text)) then
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(fun () ->
                        this.LoadPaths([ text ], false)
                    )
        } :> Task

    member private this.ToggleAbLoop() =
        if this.TryMpvCommand([| "ab-loop" |], "ab loop toggle") then
            this.ShowNotification("A-B Loop Toggled")

    member private this.ToggleFileLoop() =
        if mpvView <> null && mpvView.MpvContext <> null then
            let current = mpvView.MpvContext.GetPropertyString("loop-file")
            let newValue = if current = "no" then "inf" else "no"
            if this.TryMpvSetProperty("loop-file", newValue) then
                menuFileLoop.IsChecked <- newValue <> "no"
                this.ShowNotification(if newValue = "no" then "File Loop Off" else "File Loop On")

    member private this.TogglePlaylistLoop() =
        if mpvView <> null && mpvView.MpvContext <> null then
            let current = mpvView.MpvContext.GetPropertyString("loop-playlist")
            let newValue = if current = "no" then "inf" else "no"
            if this.TryMpvSetProperty("loop-playlist", newValue) then
                menuPlaylistLoop.IsChecked <- newValue <> "no"
                this.ShowNotification(if newValue = "no" then "Playlist Loop Off" else "Playlist Loop On")

    member private this.ApplyConfig() =
        if currentConfig.Window.W > 0 then
            this.Width <- float currentConfig.Window.W
        if currentConfig.Window.H > 0 then
            this.Height <- float currentConfig.Window.H
        if currentConfig.Window.Save && currentConfig.Window.X >= 0 && currentConfig.Window.Y >= 0 then
            this.Position <- PixelPoint(currentConfig.Window.X, currentConfig.Window.Y)
        this.ApplyTheme(currentConfig.Interface.Theme)

    member private this.PopulateAudioDevicesMenu() =
        if menuAudioDevices <> null then
            menuAudioDevices.Items.Clear()
            if playerState.AudioDevices.IsEmpty then
                let item = MenuItem(Header = "No devices found", IsEnabled = false)
                menuAudioDevices.Items.Add(item) |> ignore
            else
                let currentDevice = if mpvView <> null then mpvView.MpvContext.GetPropertyString("audio-device") else ""
                for device in playerState.AudioDevices do
                    let item = MenuItem(Header = device.Description)
                    item.IsChecked <- device.Name = currentDevice
                    item.Click.Add(fun _ -> 
                        if this.TryMpvSetProperty("audio-device", device.Name) then
                            this.ShowNotification(sprintf "Audio Device: %s" device.Description)
                    )
                    menuAudioDevices.Items.Add(item) |> ignore

    member private this.SaveWindowState() =
        if currentConfig.Window.Save then
            currentConfig <- {
                currentConfig with
                    Window = {
                        currentConfig.Window with
                            X = this.Position.X
                            Y = this.Position.Y
                            W = int this.Width
                            H = int this.Height
                    }
            }
            Config.saveConfig currentConfig

    member private this.UpdateEqualizer() =
        if mpvView <> null && mpvView.MpvContext <> null then
            try
                let gains = eqSliders |> Array.map (fun s -> string (Math.Round(s.Value, 1))) |> String.concat ":"
                // Using 'af set' to manage the equalizer filter
                this.TryMpvCommand([| "af"; "set"; sprintf "equalizer=%s" gains |], "equalizer set") |> ignore
            with ex ->
                StartupLog.writef "UpdateEqualizer ignored mpv error: %s" ex.Message

    member private this.RefreshShaders() =
        if mpvView <> null && mpvView.MpvContext <> null then
            let ctx = mpvView.MpvContext
            try
                let shaders = ctx.GetPropertyString("glsl-shaders")
                shadersSource.Clear()
                if not (String.IsNullOrWhiteSpace shaders) then
                    shaders.Split([| ','; ';' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.iter (fun s -> shadersSource.Add(Path.GetFileName(s.Trim())))
            with _ -> ()
