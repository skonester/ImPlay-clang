namespace ImPlay.Fs

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml

type App() =
    inherit Application()

    override this.Initialize() =
            AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        StartupLog.write "App.OnFrameworkInitializationCompleted entered"
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
             let args = desktop.Args |> Array.toList
             StartupLog.writef "Creating MainWindow with %d desktop args" args.Length
             let window = MainWindow(args)
             desktop.MainWindow <- window
             StartupLog.write "desktop.MainWindow assigned"
        | _ -> ()

        base.OnFrameworkInitializationCompleted()
        StartupLog.write "App.OnFrameworkInitializationCompleted completed"
