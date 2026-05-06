namespace ImPlay.App.Views

open System
open System.ComponentModel
open System.Runtime.InteropServices
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Markup.Xaml
open Avalonia.Threading
open Avalonia.Platform.Storage
open ImPlay.App.ViewModels
open ImPlay.Core.Models
open ImPlay.Core.Services

type AboutDialog() as self =
    inherit Window()
    do 
        AvaloniaXamlLoader.Load(self)
        let versionText = self.FindControl<TextBlock>("VersionText")
        if not (isNull versionText) then
            versionText.Text <- "Version 1.0.0 (F# Port)"
            
        let osText = self.FindControl<TextBlock>("OsText")
        if not (isNull osText) then
            osText.Text <- RuntimeInformation.OSDescription
            
        let archText = self.FindControl<TextBlock>("ArchText")
        if not (isNull archText) then
            archText.Text <- RuntimeInformation.ProcessArchitecture.ToString()
            
        let runtimeText = self.FindControl<TextBlock>("RuntimeText")
        if not (isNull runtimeText) then
            runtimeText.Text <- $".NET {Environment.Version}"

    member _.CheckUpdates_Click(s: obj, e: RoutedEventArgs) = ()
    member _.OpenReleases_Click(s: obj, e: RoutedEventArgs) = ()
    member _.GitHub_Click(s: obj, e: RoutedEventArgs) = ()
    member _.Releases_Click(s: obj, e: RoutedEventArgs) = ()
    member _.Issues_Click(s: obj, e: RoutedEventArgs) = ()
    member _.CloseButton_Click(s: obj, e: RoutedEventArgs) = self.Close()

type BookmarksDialog(playback: PlaybackService, settings: SettingsService) as self =
    inherit Window()
    
    let list() = self.FindControl<ListBox>("BookmarksList")
    
    let refresh() =
        match playback.CurrentFilePath with
        | Some path ->
            let bookmarks = settings.GetBookmarks(path)
            let l = list()
            if not (isNull l) then l.ItemsSource <- bookmarks
        | None -> ()

    do 
        AvaloniaXamlLoader.Load(self)
        refresh()

    member _.AddButton_Click(s: obj, e: RoutedEventArgs) =
        match playback.CurrentFilePath with
        | Some path ->
            settings.AddBookmark(path, playback.Position, "")
            refresh()
        | None -> ()

    member _.JumpButton_Click(s: obj, e: RoutedEventArgs) =
        let l = list()
        if not (isNull l) && l.SelectedIndex >= 0 then
            let b = l.SelectedItem :?> BookmarkEntry
            playback.Seek(b.Position)
            self.Close()

    member _.DeleteButton_Click(s: obj, e: RoutedEventArgs) =
        let l = list()
        if not (isNull l) && l.SelectedIndex >= 0 then
            match playback.CurrentFilePath with
            | Some path ->
                settings.RemoveBookmark(path, l.SelectedIndex)
                refresh()
            | None -> ()

type CastDialog(casting: DlnaCastService, playback: PlaybackService) as self =
    inherit Window()
    
    let list() = self.FindControl<ListBox>("DevicesList")
    let mutable devicesList : Collections.Generic.IReadOnlyList<DlnaCastDevice> = [||]
    
    let refresh() =
        let l = list()
        if not (isNull l) then l.ItemsSource <- devicesList

    do 
        AvaloniaXamlLoader.Load(self)
        task {
            let! found = casting.DiscoverAsync(TimeSpan.FromSeconds(3.0), Threading.CancellationToken.None)
            devicesList <- found
            Dispatcher.UIThread.Post(fun () -> refresh())
        } |> ignore

    member _.RefreshButton_Click(s: obj, e: RoutedEventArgs) =
        task {
            let! found = casting.DiscoverAsync(TimeSpan.FromSeconds(3.0), Threading.CancellationToken.None)
            devicesList <- found
            Dispatcher.UIThread.Post(fun () -> refresh())
        } |> ignore

    member _.DevicesList_SelectionChanged(s: obj, e: SelectionChangedEventArgs) = ()
    member _.DevicesList_DoubleTapped(s: obj, e: TappedEventArgs) =
        match list().SelectedItem with
        | :? DlnaCastDevice as device ->
            match playback.CurrentFilePath with
            | Some path ->
                casting.CastAsync(device, path, None, playback.Position, playback.Volume, Threading.CancellationToken.None) |> ignore
                self.Close()
            | None -> ()
        | _ -> ()

    member _.DisconnectButton_Click(s: obj, e: RoutedEventArgs) =
        casting.StopAsync(Threading.CancellationToken.None) |> ignore
        self.Close()

    member _.CloseButton_Click(s: obj, e: RoutedEventArgs) = self.Close()
    member _.CastButton_Click(s: obj, e: RoutedEventArgs) =
        match list().SelectedItem with
        | :? DlnaCastDevice as device ->
            match playback.CurrentFilePath with
            | Some path ->
                casting.CastAsync(device, path, None, playback.Position, playback.Volume, Threading.CancellationToken.None) |> ignore
                self.Close()
            | None -> ()
        | _ -> ()

type JumpToTimeDialog() as self =
    inherit Window()
    do AvaloniaXamlLoader.Load(self)
    
    member _.OkButton_Click(s: obj, e: RoutedEventArgs) =
        let input = self.FindControl<TextBox>("TimeInput")
        if not (isNull input) && not (String.IsNullOrWhiteSpace(input.Text)) then
            // Logic to seek would go here, usually via a result or callback
            self.Close(input.Text)
        else
            self.Close()
            
    member _.CancelButton_Click(s: obj, e: RoutedEventArgs) = self.Close()

type KeyboardShortcutsDialog() as self =
    inherit Window()
    let tryGroup (name: string) =
        let g = self.FindControl<Grid>(name)
        if isNull g then None else Some g

    let makeRow (actionText: string, keysText: string) =
        let row = Grid(ColumnDefinitions = ColumnDefinitions("*,Auto"), Margin = Thickness(0.0, 0.0, 0.0, 6.0))

        let actionBlock =
            TextBlock(
                Text = actionText,
                Foreground = Avalonia.Media.Brush.Parse("#D2CDCA"),
                FontSize = 12.0,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center)

        let keyBlock =
            TextBlock(
                Text = keysText,
                Foreground = Avalonia.Media.Brush.Parse("#9A9591"),
                FontSize = 11.0,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right)

        Grid.SetColumn(actionBlock, 0)
        Grid.SetColumn(keyBlock, 1)
        row.Children.Add(actionBlock)
        row.Children.Add(keyBlock)
        row

    let populateGroup (groupName: string) (items: (string * string) list) =
        match tryGroup groupName with
        | None -> ()
        | Some g ->
            g.RowDefinitions.Clear()
            g.Children.Clear()
            items
            |> List.iteri (fun i item ->
                g.RowDefinitions.Add(RowDefinition(GridLength.Auto))
                let row = makeRow item
                Grid.SetRow(row, i)
                g.Children.Add(row))

    do
        AvaloniaXamlLoader.Load(self)

        populateGroup "PlaybackGroup"
            [
                "Play / Pause", "Space or P"
                "Stop", "Context menu"
                "Next track", "N"
                "Previous track", "< or Shift+P"
                "Toggle loop", "L"
                "Step one frame", "."
                "Step one frame back", ","
            ]

        populateGroup "SeekingGroup"
            [
                "Seek backward", "Left"
                "Seek forward", "Right"
                "Seek backward 30s", "Ctrl + Left"
                "Seek forward 30s", "Ctrl + Right"
                "Seek backward 1m", "Down"
                "Seek forward 1m", "Up"
                "Previous chapter", "Page Up"
                "Next chapter", "Page Down"
                "Jump to time", "Ctrl + G"
            ]

        populateGroup "VolumeGroup"
            [
                "Volume up", "0"
                "Volume down", "9"
                "Mute / Unmute", "M"
            ]

        populateGroup "SpeedGroup"
            [
                "Decrease speed", "["
                "Increase speed", "]"
                "Reset speed", "\\"
            ]

        populateGroup "TracksGroup"
            [
                "Cycle audio track", "A"
                "Cycle subtitle track", "V"
                "Load subtitle", "S"
            ]

        populateGroup "WindowGroup"
            [
                "Toggle fullscreen", "F"
                "Exit fullscreen", "Esc"
                "Toggle always on top", "T"
                "Take screenshot", "Alt + I"
            ]

        populateGroup "FileGroup"
            [
                "Open media file", "O"
                "Toggle queue", "Ctrl + Q"
                "Quit", "Q"
                "Bookmarks", "B"
                "Keyboard shortcuts", "F1 or ?"
                "Toggle video renderer", "Ctrl + R"
            ]

    member _.CloseButton_Click(s: obj, e: RoutedEventArgs) = self.Close()

type SubtitleSearchDialog() as self =
    inherit Window()
    do AvaloniaXamlLoader.Load(self)
    member _.SearchButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.ResultsList_DoubleTapped(s: obj, e: TappedEventArgs) = ()
    member _.CancelButton_Click(s: obj, e: RoutedEventArgs) = self.Close()
    member _.UseButton_Click(s: obj, e: RoutedEventArgs) = ()

type SubtitleSettingsDialog(playback: PlaybackService, settings: SettingsService, searcher: SubtitleSearchService) as self =
    inherit Window()
    
    let mutable _results = ResizeArray<SubtitleSearchResult>()
    let mutable _ctSource = new Threading.CancellationTokenSource()
    
    let statusText() = self.FindControl<TextBlock>("StatusText")
    let loadingBar() = self.FindControl<ProgressBar>("LoadingBar")
    let resultsList() = self.FindControl<ListBox>("ResultsList")
    let useButton() = self.FindControl<Button>("UseButton")
    let delayLabel() = self.FindControl<TextBlock>("DelayLabel")
    let filePathBox() = self.FindControl<TextBox>("FilePathBox")
    
    let updateDelayLabel() =
        let delay = playback.SubtitleDelayMs
        let label = delayLabel()
        if not (isNull label) then label.Text <- $"{delay} ms"

    let refreshEmbeddedTracks() =
        let panel = self.FindControl<StackPanel>("EmbeddedTracksPanel")
        let list = self.FindControl<ListBox>("EmbeddedTracksList")
        if not (isNull panel) && not (isNull list) then
            let tracks = playback.GetSubtitleTracks()
            list.ItemsSource <- tracks
            panel.IsVisible <- tracks.Length > 1 // Hide if only "Off" is present

    do 
        AvaloniaXamlLoader.Load(self)
        updateDelayLabel()
        refreshEmbeddedTracks()
        
        let langBox = self.FindControl<ComboBox>("LanguageBox")
        if not (isNull langBox) then
            langBox.ItemsSource <- searcher.Languages |> List.map (fun (d, o, p) -> d)
            langBox.SelectedIndex <- 0

        let fontCombo = self.FindControl<ComboBox>("FontCombo")
        if not (isNull fontCombo) then
            fontCombo.ItemsSource <- [| "SansSerif"; "Serif"; "Monospace"; "Inter"; "Roboto"; "Arial" |]
            fontCombo.SelectedIndex <- 0
            
        let colorCombo = self.FindControl<ComboBox>("ColorCombo")
        if not (isNull colorCombo) then
            colorCombo.ItemsSource <- [| "White"; "Yellow"; "Cyan"; "Green"; "Magenta"; "Red" |]
            colorCombo.SelectedIndex <- 0

        match playback.CurrentFilePath with
        | Some path ->
            let box = filePathBox()
            if not (isNull box) then box.Text <- path
        | None -> ()

    member _.BrowseButton_Click(s: obj, e: RoutedEventArgs) =
        async {
            let! files = 
                self.StorageProvider.OpenFilePickerAsync(
                    FilePickerOpenOptions(Title = "Select Subtitle File", AllowMultiple = false))
                |> Async.AwaitTask
            match files |> Seq.tryHead with
            | Some file ->
                let path = file.TryGetLocalPath()
                if not (String.IsNullOrWhiteSpace(path)) then
                    let box = filePathBox()
                    if not (isNull box) then box.Text <- path
                    playback.LoadSubtitleFile(path)
                    refreshEmbeddedTracks()
            | None -> ()
        } |> Async.StartImmediate

    member _.SearchButton_Click(s: obj, e: RoutedEventArgs) =
        let queryBox = self.FindControl<TextBox>("SearchBox")
        let langBox = self.FindControl<ComboBox>("LanguageBox")
        if not (isNull queryBox) && not (String.IsNullOrWhiteSpace(queryBox.Text)) then
            _ctSource.Cancel()
            _ctSource <- new Threading.CancellationTokenSource()
            let ct = _ctSource.Token
            
            let status = statusText()
            let loading = loadingBar()
            if not (isNull status) then status.Text <- "Searching..."
            if not (isNull loading) then loading.IsVisible <- true
            
            let langIdx = if isNull langBox then 0 else langBox.SelectedIndex
            let lang = searcher.Languages.[Math.Max(0, langIdx)]
            let (_, os, pn) = lang
            
            task {
                try
                    let! results = searcher.SearchAsync(queryBox.Text, os, pn, ct)
                    Dispatcher.UIThread.Post(fun () ->
                        if not ct.IsCancellationRequested then
                            if not (isNull loading) then loading.IsVisible <- false
                            if results.Count = 0 then
                                if not (isNull status) then status.Text <- "No results found."
                            else
                                if not (isNull status) then status.Text <- $"Found {results.Count} results."
                                let list = resultsList()
                                if not (isNull list) then list.ItemsSource <- results
                    )
                with _ ->
                    Dispatcher.UIThread.Post(fun () ->
                        if not (isNull loading) then loading.IsVisible <- false
                        if not (isNull status) then status.Text <- "Search failed."
                    )
            } |> ignore

    member self.DownloadResultButton_Click(s: obj, e: RoutedEventArgs) =
        let btn = s :?> Button
        let result = btn.CommandParameter :?> SubtitleSearchResult
        self.DownloadAndLoad(result)

    member self.ResultsList_DoubleTapped(s: obj, e: TappedEventArgs) =
        let list = s :?> ListBox
        match list.SelectedItem with
        | :? SubtitleSearchResult as result -> self.DownloadAndLoad(result)
        | _ -> ()

    member private _.DownloadAndLoad(result: SubtitleSearchResult) =
        let status = statusText()
        if not (isNull status) then status.Text <- "Downloading..."
        
        task {
            try
                let! path = searcher.DownloadAsync(result, Threading.CancellationToken.None)
                Dispatcher.UIThread.Post(fun () ->
                    if not (isNull status) then status.Text <- "Loaded."
                    playback.LoadSubtitleFile(path)
                    let box = filePathBox()
                    if not (isNull box) then box.Text <- path
                    refreshEmbeddedTracks()
                )
            with ex ->
                Dispatcher.UIThread.Post(fun () ->
                    if not (isNull status) then status.Text <- $"Error: {ex.Message}"
                )
        } |> ignore

    member _.UseButton_Click(s: obj, e: RoutedEventArgs) = ()

    member _.SizeButton_Click(s: obj, e: RoutedEventArgs) =
        let btn = s :?> Button
        let size = btn.Tag :?> string
        match size with
        | "Small" -> playback.SetPropertyString("sub-scale", "0.8") |> ignore
        | "Medium" -> playback.SetPropertyString("sub-scale", "1.0") |> ignore
        | "Large" -> playback.SetPropertyString("sub-scale", "1.3") |> ignore
        | _ -> ()

    member _.FontCombo_SelectionChanged(s: obj, e: SelectionChangedEventArgs) =
        let combo = s :?> ComboBox
        if combo.SelectedIndex >= 0 then
            let font = combo.SelectedItem :?> string
            playback.SetPropertyString("sub-font", font) |> ignore

    member _.ColorCombo_SelectionChanged(s: obj, e: SelectionChangedEventArgs) =
        let combo = s :?> ComboBox
        if combo.SelectedIndex >= 0 then
            let color = combo.SelectedItem :?> string
            playback.SetPropertyString("sub-color", color.ToLowerInvariant()) |> ignore

    member _.DelayMinus_Click(s: obj, e: RoutedEventArgs) =
        playback.SubtitleDelayMs <- playback.SubtitleDelayMs - 100L
        updateDelayLabel()

    member _.DelayPlus_Click(s: obj, e: RoutedEventArgs) =
        playback.SubtitleDelayMs <- playback.SubtitleDelayMs + 100L
        updateDelayLabel()

    member _.DisableButton_Click(s: obj, e: RoutedEventArgs) =
        playback.SetSubtitleTrack(-1)
        refreshEmbeddedTracks()

    member _.CancelButton_Click(s: obj, e: RoutedEventArgs) = self.Close()
    member _.ApplyButton_Click(s: obj, e: RoutedEventArgs) = self.Close()

type VideoAdjustmentsDialog(playback: PlaybackService) as self =
    inherit Window()
    
    let brightnessVal() = self.FindControl<TextBlock>("BrightnessVal")
    let contrastVal() = self.FindControl<TextBlock>("ContrastVal")
    let saturationVal() = self.FindControl<TextBlock>("SaturationVal")
    let zoomVal() = self.FindControl<TextBlock>("ZoomVal")
    
    let updateValueLabels() =
        // Note: These would ideally be fetched from MPV, but for now we set initial
        ()

    do 
        AvaloniaXamlLoader.Load(self)
        let aspectCombo = self.FindControl<ComboBox>("AspectCombo")
        if not (isNull aspectCombo) then
            aspectCombo.ItemsSource <- [| "Auto"; "4:3"; "16:9"; "16:10"; "2.35:1"; "1:1" |]
            aspectCombo.SelectedIndex <- 0

    member _.BrightnessSlider_Changed(s: obj, e: RangeBaseValueChangedEventArgs) =
        let v = int e.NewValue
        playback.SetBrightness(v)
        let label = brightnessVal()
        if not (isNull label) then label.Text <- (if v >= 0 then $"+{v}" else string v)

    member _.ContrastSlider_Changed(s: obj, e: RangeBaseValueChangedEventArgs) =
        let v = int e.NewValue
        playback.SetContrast(v)
        let label = contrastVal()
        if not (isNull label) then label.Text <- (if v >= 0 then $"+{v}" else string v)

    member _.SaturationSlider_Changed(s: obj, e: RangeBaseValueChangedEventArgs) =
        let v = int e.NewValue
        playback.SetSaturation(v)
        let label = saturationVal()
        if not (isNull label) then label.Text <- (if v >= 0 then $"+{v}" else string v)

    member _.ZoomSlider_Changed(s: obj, e: RangeBaseValueChangedEventArgs) =
        let v = int e.NewValue
        playback.SetVideoZoom(v)
        let label = zoomVal()
        if not (isNull label) then label.Text <- $"{100 + v}%%"

    member _.AspectCombo_SelectionChanged(s: obj, e: SelectionChangedEventArgs) =
        let combo = s :?> ComboBox
        if combo.SelectedIndex >= 0 then
            let aspect = combo.SelectedItem :?> string
            playback.SetVideoAspect(if aspect = "Auto" then "-1" else aspect)

    member _.RotButton_Click(s: obj, e: RoutedEventArgs) =
        let btn = s :?> Button
        let rot = btn.Tag :?> string
        playback.SetVideoRotation(int rot)

    member _.ResetButton_Click(s: obj, e: RoutedEventArgs) =
        playback.SetBrightness(0)
        playback.SetContrast(0)
        playback.SetSaturation(0)
        playback.SetVideoZoom(0)
        playback.SetVideoAspect("-1")
        playback.SetVideoRotation(0)
        
        let b = self.FindControl<Slider>("BrightnessSlider")
        if not (isNull b) then b.Value <- 0.0
        let c = self.FindControl<Slider>("ContrastSlider")
        if not (isNull c) then c.Value <- 0.0
        let sat = self.FindControl<Slider>("SaturationSlider")
        if not (isNull sat) then sat.Value <- 0.0
        let z = self.FindControl<Slider>("ZoomSlider")
        if not (isNull z) then z.Value <- 0.0
        let a = self.FindControl<ComboBox>("AspectCombo")
        if not (isNull a) then a.SelectedIndex <- 0

    member _.CancelButton_Click(s: obj, e: RoutedEventArgs) = self.Close()
    member _.OkButton_Click(s: obj, e: RoutedEventArgs) = self.Close()
