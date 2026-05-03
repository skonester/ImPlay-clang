namespace ImPlay.Fs

open System
open Avalonia.Media
open Avalonia.Data.Converters

module Domain =
    
    type PlaybackStatus =
        | Playing of filename: string
        | Paused of filename: string
        | Stopped
        | Idle

    type InterfaceSettings = {
        Lang: string
        Theme: string
        Scale: float32
        Fps: int
        Docking: bool
        Viewports: bool
        Rounding: bool
        Shadow: bool
    }

    type FontSettings = {
        Path: string
        Size: int
        GlyphRange: int
    }

    type MpvSettings = {
        UseConfig: bool
        UseWid: bool
        WatchLater: bool
        Volume: int
    }

    type WindowSettings = {
        Save: bool
        Single: bool
        X: int
        Y: int
        W: int
        H: int
    }

    type DebugSettings = {
        LogLevel: string
        LogLimit: int
    }

    type RecentSettings = {
        Limit: int
        SpaceToPlayLast: bool
    }

    type RecentItem = {
        Path: string
        Title: string
    }

    type PlaylistItem = {
        Id: int
        Path: string
        Title: string
        IsPlaying: bool
    }

    type PlayerConfig = {
        Interface: InterfaceSettings
        Font: FontSettings
        Mpv: MpvSettings
        Window: WindowSettings
        Debug: DebugSettings
        Recent: RecentSettings
        RecentFiles: RecentItem list
    }

    let defaultConfig = {
        Interface = { Lang = "en-US"; Theme = "light"; Scale = 0.0f; Fps = 30; Docking = false; Viewports = false; Rounding = true; Shadow = true }
        Font = { Path = ""; Size = 13; GlyphRange = 0 }
        Mpv = { UseConfig = false; UseWid = false; WatchLater = false; Volume = 100 }
        Window = { Save = false; Single = false; X = 0; Y = 0; W = 0; H = 0 }
        Debug = { LogLevel = "status"; LogLimit = 500 }
        Recent = { Limit = 10; SpaceToPlayLast = false }
        RecentFiles = []
    }

    type ChapterItem = {
        Id: int
        Title: string
        Time: float
    }

    type TrackItem = {
        Id: int
        Type: string
        Title: string
        Lang: string
        Codec: string
        Details: string
        Selected: bool
        IsSecondary: bool
    }

    type AudioDeviceItem = {
        Name: string
        Description: string
    }

    type PlayerState = {
        Status: PlaybackStatus
        Config: PlayerConfig
        Playlist: PlaylistItem list
        CurrentIndex: int
        Volume: int
        Mute: bool
        Fullscreen: bool
        TimePos: float
        Duration: float
        Chapters: ChapterItem list
        Tracks: TrackItem list
        AudioDevices: AudioDeviceItem list
        Profiles: string list
    }

    let initialState = {
        Status = Idle
        Config = defaultConfig
        Playlist = []
        CurrentIndex = 0
        Volume = 100
        Mute = false
        Fullscreen = false
        TimePos = 0.0
        Duration = 0.0
        Chapters = []
        Tracks = []
        AudioDevices = []
        Profiles = []
    }

type LogEntry = {
    Message: string
    Color: string
}

type PropertyEntry = {
    Name: string
    Value: string
}

type CommandEntry = {
    Name: string
    Args: string
}

type CommandItem = {
    Title: string
    Tooltip: string
    Label: string
    Id: int
    Action: unit -> unit
}

type BoolToBrushConverter() =
    interface IValueConverter with
        member _.Convert(value, _, _, _) =
            match value with
            | :? bool as b -> if b then SolidColorBrush(Color.Parse("#4CAF50")) :> obj else SolidColorBrush(Color.Parse("#FFFFFF")) :> obj
            | _ -> null
        member _.ConvertBack(_, _, _, _) = raise (NotImplementedException())
