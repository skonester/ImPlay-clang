namespace ImPlay.Fs

open System
open System.IO
open System.Collections.Generic
open System.Text.Json

module Lang =
    type LangData = {
        Code: string
        Title: string
        Entries: Dictionary<string, string>
    }

    let mutable private currentLang = "en-US"
    let mutable private fallbackLang = "en-US"
    let private langs = Dictionary<string, LangData>()
    let private currentEntries = Dictionary<string, string>()

    let loadLangFiles (basePath: string) =
        try
            let langDir = Path.Combine(basePath, "resources", "romfs", "lang")
            if Directory.Exists(langDir) then
                Directory.EnumerateFiles(langDir, "*.json")
                |> Seq.iter (fun file ->
                    try
                        let json = File.ReadAllText(file)
                        let doc = JsonDocument.Parse(json)
                        let root = doc.RootElement
                        let code = root.GetProperty("code").GetString()
                        let title = root.GetProperty("title").GetString()
                        let entriesElem = root.GetProperty("entries")
                        
                        let entries = Dictionary<string, string>()
                        for prop in entriesElem.EnumerateObject() do
                            entries.[prop.Name] <- prop.Value.GetString()
                        
                        langs.[code] <- { Code = code; Title = title; Entries = entries }
                    with ex ->
                        printfn "Error loading lang file %s: %s" file ex.Message
                )
        with ex ->
            printfn "Error accessing lang directory: %s" ex.Message

    let setLang code =
        currentLang <- code
        currentEntries.Clear()
        
        // Load fallback first
        if langs.ContainsKey(fallbackLang) then
            for kv in langs.[fallbackLang].Entries do
                currentEntries.[kv.Key] <- kv.Value
        
        // Overwrite with current lang
        if langs.ContainsKey(currentLang) then
            for kv in langs.[currentLang].Entries do
                currentEntries.[kv.Key] <- kv.Value

    let i18n key =
        match currentEntries.TryGetValue(key) with
        | true, value -> value
        | _ -> key

    let getAvailableLangs () =
        langs.Values |> Seq.map (fun l -> l.Code, l.Title) |> Seq.toList
