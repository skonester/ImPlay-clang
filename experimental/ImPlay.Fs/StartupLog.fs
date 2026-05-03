namespace ImPlay.Fs

open System
open System.IO
open System.Text

module StartupLog =

    let private lockObj = obj()
    let mutable private logPath: string option = None

    let private ensureDir (path: string) =
        if not (Directory.Exists(path)) then
            Directory.CreateDirectory(path) |> ignore

    let resolvePath () =
        let exeDir = AppDomain.CurrentDomain.BaseDirectory
        let portableDir = Path.Combine(exeDir, "portable_config")
        let dataDir =
            if Directory.Exists(portableDir) then
                portableDir
            else
                let appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                Path.Combine(appData, "implay")

        ensureDir dataDir
        Path.Combine(dataDir, "startup.log")

    let initialize () =
        lock lockObj (fun () ->
            let path = resolvePath()
            logPath <- Some path
            let header =
                sprintf
                    "%s\n=== ImPlay.Fs startup %s ===\nBaseDir=%s\n"
                    (String.replicate 72 "-")
                    (DateTimeOffset.Now.ToString("O"))
                    AppDomain.CurrentDomain.BaseDirectory
            File.AppendAllText(path, header, Encoding.UTF8)
            path)

    let path () =
        match logPath with
        | Some path -> path
        | None -> initialize()

    let write (message: string) =
        lock lockObj (fun () ->
            let line = sprintf "[%s] %s\n" (DateTimeOffset.Now.ToString("O")) message
            try
                File.AppendAllText(path(), line, Encoding.UTF8)
            with _ ->
                ())

    let writef fmt = Printf.ksprintf write fmt

    let exception' (context: string) (ex: exn) =
        writef "%s failed: %s\n%s" context ex.Message ex.StackTrace
