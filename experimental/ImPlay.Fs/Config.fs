namespace ImPlay.Fs

open System
open System.IO
open IniParser
open IniParser.Model
open ImPlay.Fs.Domain

module Config =

    let getDataPath () =
        let exeDir = AppDomain.CurrentDomain.BaseDirectory
        let portableDir = Path.Combine(exeDir, "portable_config")
        if Directory.Exists(portableDir) then
            portableDir
        else
            let appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            let path = Path.Combine(appData, "implay")
            if not (Directory.Exists(path)) then
                Directory.CreateDirectory(path) |> ignore
            path

    let loadConfig () =
        let path = getDataPath()
        let configFile = Path.Combine(path, "implay.conf")
        let parser = FileIniDataParser()
        
        if not (File.Exists(configFile)) then
            defaultConfig
        else
            let data = parser.ReadFile(configFile)
            let def = defaultConfig
            
            let getVal section key (defaultVal: 'a) =
                if data.Sections.ContainsSection(section) && data.Sections.[section].ContainsKey(key) then
                    data.Sections.[section].[key]
                else
                    string defaultVal

            let parseBool s defValue =
                match (string s).ToLower() with
                | "true" | "yes" | "1" -> true
                | "false" | "no" | "0" -> false
                | _ -> defValue

            let parseInt (s: string) defValue =
                match Int32.TryParse(s) with
                | (true, v) -> v
                | _ -> defValue

            let parseFloat (s: string) defValue =
                match Single.TryParse(s) with
                | (true, v) -> v
                | _ -> defValue

            let recentFiles = 
                if data.Sections.ContainsSection("recent") then
                    data.Sections.["recent"] 
                    |> Seq.filter (fun keyData -> keyData.KeyName.StartsWith("file-"))
                    |> Seq.map (fun keyData -> 
                        let value = keyData.Value
                        let parts = value.Split('|')
                        if parts.Length > 1 then
                            { Path = parts.[0]; Title = parts.[1] }
                        else
                            { Path = value; Title = value }
                    )
                    |> Seq.toList
                else
                    []

            {
                Interface = {
                    Lang = getVal "interface" "lang" def.Interface.Lang
                    Theme = getVal "interface" "theme" def.Interface.Theme
                    Scale = parseFloat (getVal "interface" "scale" "") def.Interface.Scale
                    Fps = parseInt (getVal "interface" "fps" "") def.Interface.Fps
                    Docking = parseBool (getVal "interface" "docking" "") def.Interface.Docking
                    Viewports = parseBool (getVal "interface" "viewports" "") def.Interface.Viewports
                    Rounding = parseBool (getVal "interface" "rounding" "") def.Interface.Rounding
                    Shadow = parseBool (getVal "interface" "shadow" "") def.Interface.Shadow
                }
                Font = {
                    Path = getVal "font" "path" def.Font.Path
                    Size = parseInt (getVal "font" "size" "") def.Font.Size
                    GlyphRange = parseInt (getVal "font" "glyph-range" "") def.Font.GlyphRange
                }
                Mpv = {
                    UseConfig = parseBool (getVal "mpv" "config" "") def.Mpv.UseConfig
                    UseWid = parseBool (getVal "mpv" "wid" "") def.Mpv.UseWid
                    WatchLater = parseBool (getVal "mpv" "watch-later" "") def.Mpv.WatchLater
                    Volume = parseInt (getVal "mpv" "volume" "") def.Mpv.Volume
                }
                Window = {
                    Save = parseBool (getVal "window" "save" "") def.Window.Save
                    Single = parseBool (getVal "window" "single" "") def.Window.Single
                    X = parseInt (getVal "window" "x" "") def.Window.X
                    Y = parseInt (getVal "window" "y" "") def.Window.Y
                    W = parseInt (getVal "window" "w" "") def.Window.W
                    H = parseInt (getVal "window" "h" "") def.Window.H
                }
                Debug = {
                    LogLevel = getVal "debug" "log-level" def.Debug.LogLevel
                    LogLimit = parseInt (getVal "debug" "log-limit" "") def.Debug.LogLimit
                }
                Recent = {
                    Limit = parseInt (getVal "recent" "limit" "") def.Recent.Limit
                    SpaceToPlayLast = parseBool (getVal "recent" "space-to-play-last" "") def.Recent.SpaceToPlayLast
                }
                RecentFiles = recentFiles
            }

    let saveConfig (config: PlayerConfig) =
        let path = getDataPath()
        let configFile = Path.Combine(path, "implay.conf")
        let parser = FileIniDataParser()
        let data = IniData()

        let setVal section key value =
            if not (data.Sections.ContainsSection(section)) then
                data.Sections.AddSection(section) |> ignore
            data.Sections.[section].[key] <- string value

        setVal "interface" "lang" config.Interface.Lang
        setVal "interface" "theme" config.Interface.Theme
        setVal "interface" "scale" config.Interface.Scale
        setVal "interface" "fps" config.Interface.Fps
        setVal "interface" "docking" config.Interface.Docking
        setVal "interface" "viewports" config.Interface.Viewports
        setVal "interface" "rounding" config.Interface.Rounding
        setVal "interface" "shadow" config.Interface.Shadow
        
        setVal "font" "path" config.Font.Path
        setVal "font" "size" config.Font.Size
        setVal "font" "glyph-range" config.Font.GlyphRange
        
        setVal "mpv" "config" config.Mpv.UseConfig
        setVal "mpv" "wid" config.Mpv.UseWid
        setVal "mpv" "watch-later" config.Mpv.WatchLater
        setVal "mpv" "volume" config.Mpv.Volume
        
        setVal "window" "save" config.Window.Save
        setVal "window" "single" config.Window.Single
        setVal "window" "x" config.Window.X
        setVal "window" "y" config.Window.Y
        setVal "window" "w" config.Window.W
        setVal "window" "h" config.Window.H
        
        setVal "debug" "log-level" config.Debug.LogLevel
        setVal "debug" "log-limit" config.Debug.LogLimit
        
        setVal "recent" "limit" config.Recent.Limit
        setVal "recent" "space-to-play-last" config.Recent.SpaceToPlayLast

        // Save recent files
        config.RecentFiles 
        |> List.iteri (fun i item -> 
            let value = if item.Path = item.Title then item.Path else sprintf "%s|%s" item.Path item.Title
            setVal "recent" (sprintf "file-%d" i) value
        )

        parser.WriteFile(configFile, data)
