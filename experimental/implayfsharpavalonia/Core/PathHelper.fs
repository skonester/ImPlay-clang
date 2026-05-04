namespace ImPlay.Core.Helpers

open System
open System.IO

module PathHelper =
    let GetConfigDir() =
        let exeDir = AppDomain.CurrentDomain.BaseDirectory
        let portableDir = Path.Combine(exeDir, "portable_config")
        
        if Directory.Exists(portableDir) then
            portableDir
        else
            let appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            let path = Path.Combine(appData, "ImPlay")
            if not (Directory.Exists(path)) then
                Directory.CreateDirectory(path) |> ignore
            path
