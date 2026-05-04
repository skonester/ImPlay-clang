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
open ImPlay.App.ViewModels

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

type BookmarksDialog() as self =
    inherit Window()
    do AvaloniaXamlLoader.Load(self)
    member _.AddButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.JumpButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.DeleteButton_Click(s: obj, e: RoutedEventArgs) = ()

type CastDialog() as self =
    inherit Window()
    do AvaloniaXamlLoader.Load(self)
    member _.RefreshButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.DevicesList_SelectionChanged(s: obj, e: SelectionChangedEventArgs) = ()
    member _.DevicesList_DoubleTapped(s: obj, e: TappedEventArgs) = ()
    member _.DisconnectButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.CloseButton_Click(s: obj, e: RoutedEventArgs) = self.Close()
    member _.CastButton_Click(s: obj, e: RoutedEventArgs) = ()

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
    do AvaloniaXamlLoader.Load(self)
    member _.CloseButton_Click(s: obj, e: RoutedEventArgs) = self.Close()

type SubtitleSearchDialog() as self =
    inherit Window()
    do AvaloniaXamlLoader.Load(self)
    member _.SearchButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.ResultsList_DoubleTapped(s: obj, e: TappedEventArgs) = ()
    member _.CancelButton_Click(s: obj, e: RoutedEventArgs) = self.Close()
    member _.UseButton_Click(s: obj, e: RoutedEventArgs) = ()

type SubtitleSettingsDialog() as self =
    inherit Window()
    do AvaloniaXamlLoader.Load(self)
    member _.BrowseButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.SearchButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.DownloadResultButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.ResultsList_DoubleTapped(s: obj, e: TappedEventArgs) = ()
    member _.UseButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.SizeButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.FontCombo_SelectionChanged(s: obj, e: SelectionChangedEventArgs) = ()
    member _.ColorCombo_SelectionChanged(s: obj, e: SelectionChangedEventArgs) = ()
    member _.DelayMinus_Click(s: obj, e: RoutedEventArgs) = ()
    member _.DelayPlus_Click(s: obj, e: RoutedEventArgs) = ()
    member _.DisableButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.CancelButton_Click(s: obj, e: RoutedEventArgs) = self.Close()
    member _.ApplyButton_Click(s: obj, e: RoutedEventArgs) = self.Close()

type VideoAdjustmentsDialog() as self =
    inherit Window()
    do AvaloniaXamlLoader.Load(self)
    member _.BrightnessSlider_Changed(s: obj, e: RangeBaseValueChangedEventArgs) = ()
    member _.ContrastSlider_Changed(s: obj, e: RangeBaseValueChangedEventArgs) = ()
    member _.SaturationSlider_Changed(s: obj, e: RangeBaseValueChangedEventArgs) = ()
    member _.ZoomSlider_Changed(s: obj, e: RangeBaseValueChangedEventArgs) = ()
    member _.AspectCombo_SelectionChanged(s: obj, e: SelectionChangedEventArgs) = ()
    member _.RotButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.ResetButton_Click(s: obj, e: RoutedEventArgs) = ()
    member _.CancelButton_Click(s: obj, e: RoutedEventArgs) = self.Close()
    member _.OkButton_Click(s: obj, e: RoutedEventArgs) = self.Close()
