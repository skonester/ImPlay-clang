namespace ImPlay.Core.Services

open System
open System.Collections.Generic
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open ImPlay.Core.Helpers

type SubtitleEntry() =
    member val FilePath : string option = None with get, set
    member val FontSize : string = "Medium" with get, set
    member val Font : string = "SansSerif" with get, set
    member val Color : string = "White" with get, set
    member val DelayMs : int64 = 0L with get, set
    member val EmbeddedTrackId : int option = None with get, set

type BookmarkEntry() =
    member val PositionSeconds : double = 0.0 with get, set
    member val Label : string = "" with get, set
    member self.Position = TimeSpan.FromSeconds(self.PositionSeconds)
    member self.FormattedTime =
        let t = self.Position
        if t.TotalHours >= 1.0 then
            $"{int t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
        else
            $"{t.Minutes}:{t.Seconds:D2}"

type SettingsFile() =
    member val ResumePositions = Dictionary<string, double>() with get, set
    member val ResumeDurations = Dictionary<string, double>() with get, set
    member val RecentFiles = List<string>() with get, set
    member val SubtitleSettings = Dictionary<string, SubtitleEntry>() with get, set
    member val Bookmarks = Dictionary<string, List<BookmarkEntry>>() with get, set
    member val LastVolume = 80 with get, set
    member val LastSpeed = 1.0f with get, set
    member val SeekStep = 5 with get, set

type SettingsService() =
    let _maxRecentFiles = 12
    let _settingsPath = Path.Combine(PathHelper.GetConfigDir(), "settings.json")
    
    let mutable _resumePositions = Dictionary<string, double>()
    let mutable _resumeDurations = Dictionary<string, double>()
    let mutable _recentFiles = List<string>()
    let mutable _subtitleSettings = Dictionary<string, SubtitleEntry>()
    let mutable _bookmarks = Dictionary<string, List<BookmarkEntry>>()
    
    let mutable _lastVolume = 80
    let mutable _lastSpeed = 1.0f
    let mutable _seekStep = 5

    let keyForFile (filePath: string) =
        let normalized = Path.GetFullPath(filePath)
        let bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized))
        Convert.ToHexString(bytes).ToLowerInvariant()

    let loadSettings() =
        if not (File.Exists(_settingsPath)) then SettingsFile()
        else
            try
                let json = File.ReadAllText(_settingsPath)
                JsonSerializer.Deserialize<SettingsFile>(json)
                |> Option.ofObj
                |> Option.defaultValue (SettingsFile())
            with _ -> SettingsFile()

    let save() =
        let settings = SettingsFile()
        settings.ResumePositions <- _resumePositions
        settings.ResumeDurations <- _resumeDurations
        settings.RecentFiles <- _recentFiles
        settings.SubtitleSettings <- _subtitleSettings
        settings.Bookmarks <- _bookmarks
        settings.LastVolume <- _lastVolume
        settings.LastSpeed <- _lastSpeed
        settings.SeekStep <- _seekStep
        
        let options = JsonSerializerOptions(WriteIndented = true)
        let json = JsonSerializer.Serialize(settings, options)
        File.WriteAllText(_settingsPath, json)

    do
        let settings = loadSettings()
        _resumePositions <- settings.ResumePositions
        _resumeDurations <- settings.ResumeDurations
        _recentFiles <- settings.RecentFiles
        _subtitleSettings <- settings.SubtitleSettings
        _bookmarks <- settings.Bookmarks
        _lastVolume <- settings.LastVolume
        _lastSpeed <- settings.LastSpeed
        _seekStep <- settings.SeekStep

    member _.RecentFiles = _recentFiles :> IReadOnlyList<string>
    member _.LastVolume = _lastVolume
    member _.LastSpeed = _lastSpeed
    member _.SeekStep = _seekStep

    member _.SaveSessionPreferences(volume, speed, seekStep) =
        _lastVolume <- Math.Clamp(volume, 0, 150)
        _lastSpeed <- Math.Clamp(speed, 0.25f, 4.0f)
        _seekStep <- if List.contains seekStep [5; 10; 30] then seekStep else 5
        save()

    member _.AddRecentFile(filePath) =
        _recentFiles.Remove(filePath) |> ignore
        _recentFiles.Insert(0, filePath)
        if _recentFiles.Count > _maxRecentFiles then
            _recentFiles.RemoveRange(_maxRecentFiles, _recentFiles.Count - _maxRecentFiles)
        save()

    member _.RemoveRecentFile(filePath) =
        if _recentFiles.Remove(filePath) then save()

    member _.GetResumePosition(filePath) =
        match _resumePositions.TryGetValue(keyForFile filePath) with
        | true, seconds -> TimeSpan.FromSeconds(seconds)
        | _ -> TimeSpan.Zero

    member _.SaveResumePosition(filePath: string option, position: TimeSpan, duration: TimeSpan) =
        filePath |> Option.iter (fun path ->
            if not (String.IsNullOrWhiteSpace path) then
                let key = keyForFile path
                if position.TotalSeconds < 5.0 || (duration > TimeSpan.Zero && duration - position < TimeSpan.FromSeconds(5.0)) then
                    _resumePositions.Remove(key) |> ignore
                    _resumeDurations.Remove(key) |> ignore
                else
                    _resumePositions.[key] <- position.TotalSeconds
                    if duration > TimeSpan.Zero then
                        _resumeDurations.[key] <- duration.TotalSeconds
                save()
        )

    member _.GetResumeInfo(filePath) =
        let key = keyForFile filePath
        match _resumePositions.TryGetValue(key) with
        | true, pos ->
            let pct = 
                match _resumeDurations.TryGetValue(key) with
                | true, dur when dur > 0.0 -> Math.Clamp(pos / dur * 100.0, 0.0, 100.0)
                | _ -> -1.0
            (TimeSpan.FromSeconds(pos), pct)
        | _ -> (TimeSpan.Zero, -1.0)

    member _.ClearResumePosition(filePath: string option) =
        filePath |> Option.iter (fun path ->
            if not (String.IsNullOrWhiteSpace path) then
                if _resumePositions.Remove(keyForFile path) then save()
        )

    member _.GetBookmarks(filePath) =
        match _bookmarks.TryGetValue(keyForFile filePath) with
        | true, list -> list :> IReadOnlyList<BookmarkEntry>
        | _ -> List<BookmarkEntry>() :> IReadOnlyList<BookmarkEntry>

    member _.AddBookmark(filePath, position: TimeSpan, label) =
        let key = keyForFile filePath
        if not (_bookmarks.ContainsKey(key)) then _bookmarks.[key] <- List<BookmarkEntry>()
        let entry = BookmarkEntry()
        entry.PositionSeconds <- position.TotalSeconds
        entry.Label <- label
        _bookmarks.[key].Add(entry)
        _bookmarks.[key].Sort(fun a b -> a.PositionSeconds.CompareTo(b.PositionSeconds))
        save()

    member _.RemoveBookmark(filePath, index) =
        let key = keyForFile filePath
        match _bookmarks.TryGetValue(key) with
        | true, list when index >= 0 && index < list.Count ->
            list.RemoveAt(index)
            save()
        | _ -> ()

    member _.RenameBookmark(filePath, index, newLabel) =
        let key = keyForFile filePath
        match _bookmarks.TryGetValue(key) with
        | true, list when index >= 0 && index < list.Count ->
            list.[index].Label <- newLabel
            save()
        | _ -> ()

    member _.GetSubtitleSettings(filePath) =
        match _subtitleSettings.TryGetValue(keyForFile filePath) with
        | true, entry -> Some entry
        | _ -> None

    member _.SaveSubtitleSettings(filePath, entry) =
        _subtitleSettings.[keyForFile filePath] <- entry
        save()

    member _.ClearSubtitleSettings(filePath) =
        if _subtitleSettings.Remove(keyForFile filePath) then save()
