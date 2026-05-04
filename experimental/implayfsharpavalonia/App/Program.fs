namespace ImPlay.App

open System
open Avalonia
open Avalonia.Win32
open Avalonia.X11
open ImPlay.Core.Services

module Program =
    [<EntryPoint>]
    let main args =
        StartupLogger.Log("App starting...")

        if String.IsNullOrEmpty(Environment.GetEnvironmentVariable("SESSION_MANAGER")) then
            Environment.SetEnvironmentVariable("SESSION_MANAGER", "")

        try
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(Win32PlatformOptions(RenderingMode = [| Win32RenderingMode.Vulkan; Win32RenderingMode.Wgl |]))
                .With(X11PlatformOptions(RenderingMode = [| X11RenderingMode.Vulkan; X11RenderingMode.Glx |], EnableIme = false))
                .WithInterFont()
                .LogToTrace()
                .StartWithClassicDesktopLifetime(args)
        with ex ->
            StartupLogger.LogException("Main loop", ex)
            reraise()
