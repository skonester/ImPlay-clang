namespace ImPlay.Core.Models

open System

type MediaState() =
    member val FilePath : string option = None with get, set
    member val Position : TimeSpan = TimeSpan.Zero with get, set
    member val Duration : TimeSpan = TimeSpan.Zero with get, set
    member val IsPlaying : bool = false with get, set
    member val IsMuted : bool = false with get, set
    member val Volume : int = 80 with get, set
    member val Speed : float32 = 1.0f with get, set
    member val IsLooping : bool = false with get, set

type VideoFrameData = {
    Width: int
    Height: int
    Stride: int
    Data: IntPtr
}

type MediaTrack = {
    Id: int
    Name: string
    IsSelected: bool
}

type SubtitleLine = { 
    Start : TimeSpan
    End : TimeSpan
    Text : string 
}

type SubtitleSearchResult = {
    Source: string
    Title: string
    FileName: string
    Language: string
    Format: string
    DownloadUrl: string
    Downloads: int
}

type DlnaCastDevice = {
    Id : string
    Name : string
    DescriptionUrl : Uri
    ControlUrl : Uri
    RenderingControlUrl : Uri option
}
