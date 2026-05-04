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

type SubtitleSearchResult() =
    member val Id : string = "" with get, set
    member val Title : string = "" with get, set
    member val FileName : string = "" with get, set
    member val Language : string = "" with get, set
    member val Format : string = "" with get, set
    member val Source : string = "" with get, set
    member val Downloads : int = 0 with get, set
    member val Url : string = "" with get, set
