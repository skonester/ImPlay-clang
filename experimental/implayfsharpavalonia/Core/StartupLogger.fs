namespace ImPlay.Core.Services

open System
open System.IO
open System.Text
open ImPlay.Core.Helpers

module StartupLogger =
    let private lockObj = obj()
    let mutable private _logPath : string option = None

    let initialize() =
        lock lockObj (fun () ->
            let configDir = PathHelper.GetConfigDir()
            let path = Path.Combine(configDir, "startup.log")
            _logPath <- Some path
            
            let sb = StringBuilder()
            sb.AppendLine(String('-', 72)) |> ignore
            sb.AppendLine($"=== ImPlay startup {DateTimeOffset.Now:O} ===") |> ignore
            sb.AppendLine($"BaseDir={AppDomain.CurrentDomain.BaseDirectory}") |> ignore
            sb.AppendLine() |> ignore
            
            File.AppendAllText(path, sb.ToString(), Encoding.UTF8)
            path
        )

    let logPath() =
        match _logPath with
        | Some p -> p
        | None -> initialize()

    let log (message: string) =
        lock lockObj (fun () ->
            let line = $"[{DateTimeOffset.Now:O}] {message}\n"
            try
                File.AppendAllText(logPath(), line, Encoding.UTF8)
            with _ -> ()
        )

    let logException (context: string) (ex: Exception) =
        log $"{context} failed: {ex.Message}\n{ex.StackTrace}"
