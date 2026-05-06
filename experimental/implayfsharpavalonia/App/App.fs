namespace ImPlay.App

open System
open System.IO
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml
open ImPlay.Core.Services
open ImPlay.App.ViewModels
open ImPlay.App.Views

type App() as self =
    inherit Application()

    let tryGetStartupFilePath (args: string[]) =
        args 
        |> Array.tryFind (fun arg ->
            if String.IsNullOrWhiteSpace(arg) || arg.StartsWith('-') then false
            else
                let path =
                    match Uri.TryCreate(arg, UriKind.Absolute) with
                    | true, uri when uri.IsFile -> uri.LocalPath
                    | _ -> arg
                File.Exists(path)
        )

    override _.Initialize() =
        AvaloniaXamlLoader.Load(self)

    override _.OnFrameworkInitializationCompleted() =
        match self.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            let playback = new PlaybackService()
            let settings = new SettingsService()
            let casting = new DlnaCastService()
            let searcher = new SubtitleSearchService()
            let vm = new MainViewModel(playback, settings, casting)
            
            let window = new MainWindow(playback, settings, casting, searcher)
            window.DataContext <- vm
            desktop.MainWindow <- window
            
            match tryGetStartupFilePath desktop.Args with
            | Some filePath ->
                window.Opened.Add(fun _ ->
                    vm.OpenAsync(filePath)
                )
            | _ -> ()
        | _ -> ()
        
        base.OnFrameworkInitializationCompleted()
