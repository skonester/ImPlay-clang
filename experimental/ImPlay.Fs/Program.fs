namespace ImPlay.Fs

open System
open System.IO
open System.Runtime.InteropServices
open Avalonia

module Program =

    [<DllImport("user32.dll", CharSet = CharSet.Unicode)>]
    extern int private MessageBoxW(IntPtr hWnd, string text, string caption, uint options)

    let private showFatalStartupDialog (message: string) =
        if OperatingSystem.IsWindows() then
            try
                MessageBoxW(IntPtr.Zero, message, "ImPlay.Fs Startup Failure", 0x00000010u) |> ignore
            with _ ->
                ()

    [<CompiledName "BuildAvaloniaApp">] 
    let buildAvaloniaApp () = 
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(areas = Array.empty)

    [<EntryPoint; STAThread>]
    let main argv =
        let logPath = StartupLog.initialize()
        try
            StartupLog.write "Program.main entered"
            StartupLog.writef "Args: %s" (String.Join(" ", argv))
            printfn "=== ImPlay.Fs Startup Debug ==="
            printfn "Startup log: %s" logPath
            
            let resourcesDir = Platform.getResourcesPath()
            StartupLog.writef "Resources dir: %s" resourcesDir
            printfn "Resources dir: %s" resourcesDir
            
            Lang.loadLangFiles resourcesDir
            StartupLog.write "Language files loaded"
            Platform.setupFileAssociations()
            StartupLog.write "File association setup completed"
            
            let config = Config.loadConfig()
            StartupLog.writef "Config loaded: Single=%b, Lang=%s" config.Window.Single config.Interface.Lang
            printfn "Config loaded: Single=%b, Lang=%s" config.Window.Single config.Interface.Lang
            
            let args = Cli.parse (argv |> Array.toList)
            StartupLog.writef "Args parsed: Paths=%d, Playlists=%d, Subtitles=%d, Options=%A" args.Paths.Length args.PlaylistFiles.Length args.SubtitleFiles.Length args.Options
            printfn "Args parsed: Paths=%d" args.Paths.Length
            
            // Set language before starting UI
            Lang.setLang config.Interface.Lang
            StartupLog.writef "Language set: %s" config.Interface.Lang
            
            let singleInstanceOptIn =
                args.Options
                |> List.exists (fun (key, value) ->
                    key = "single-instance" &&
                    match value.ToLowerInvariant() with
                    | "yes" | "true" | "1" | "on" -> true
                    | _ -> false)

            StartupLog.writef "IPC opt-in=%b, Config.Single=%b" singleInstanceOptIn config.Window.Single

            if singleInstanceOptIn && config.Window.Single && not args.Paths.IsEmpty then
                StartupLog.write "Explicit single-instance IPC requested; checking for existing instance"
                printfn "Checking for existing instance..."
                if Ipc.sendIpc args.Paths then
                    StartupLog.write "Paths sent to existing instance. Exiting by explicit IPC request."
                    printfn "Paths sent to existing instance. Exiting."
                    0
                else
                    StartupLog.write "No existing instance found after explicit IPC request; starting Avalonia"
                    printfn "No existing instance found. Starting new instance."
                    printfn "Starting Avalonia app..."
                    StartupLog.write "Calling StartWithClassicDesktopLifetime"
                    buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
            else
                StartupLog.write "Starting Avalonia app; IPC disabled by default for experimental builds"
                printfn "Starting Avalonia app..."
                buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
        with ex ->
            StartupLog.exception' "Program.main" ex
            printfn "!!! FATAL ERROR during startup !!!"
            printfn "Exception: %s" ex.Message
            printfn "Stack trace:\n%s" ex.StackTrace
            showFatalStartupDialog (sprintf "ImPlay.Fs failed during startup.\n\n%s\n\nStartup log:\n%s" ex.Message logPath)
            1

