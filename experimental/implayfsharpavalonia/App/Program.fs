namespace ImPlay.App

open System
open Avalonia
open Avalonia.Win32
open Avalonia.X11
open ImPlay.Core.Services

module Program =
    [<EntryPoint>]
    let main args =
        try StartupLogger.log("App starting...") with _ -> ()

        if String.IsNullOrEmpty(Environment.GetEnvironmentVariable("SESSION_MANAGER")) then
            Environment.SetEnvironmentVariable("SESSION_MANAGER", "")

        try
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(Win32PlatformOptions(RenderingMode = [| Win32RenderingMode.Wgl; Win32RenderingMode.Vulkan |]))
                .With(X11PlatformOptions(RenderingMode = [| X11RenderingMode.Glx; X11RenderingMode.Vulkan |], EnableIme = false))
                .WithInterFont()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(args)
        with ex ->
            StartupLogger.logException "Main loop" ex
            reraise()
