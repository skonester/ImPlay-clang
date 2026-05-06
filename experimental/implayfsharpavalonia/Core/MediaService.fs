namespace ImPlay.Core.Services

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Text.RegularExpressions

type DiscKind = Dvd | Bluray

module MediaService =
    let VideoExtensions = [
        "yuv"; "y4m"; "m2ts"; "m2t"; "mts"; "mtv"; "ts"; "tsv"; "tsa"; "tts"; "trp"; "mpeg"; "mpg"
        "mpe"; "mpeg2"; "m1v"; "m2v"; "mp2v"; "mpv"; "mpv2"; "mod"; "vob"; "vro"; "evob"; "evo"; "mpeg4"
        "m4v"; "mp4"; "mp4v"; "mpg4"; "h264"; "avc"; "x264"; "264"; "hevc"; "h265"; "x265"; "265"; "ogv"
        "ogm"; "ogx"; "mkv"; "mk3d"; "webm"; "avi"; "vfw"; "divx"; "3iv"; "xvid"; "nut"; "flic"; "fli"
        "flc"; "nsv"; "gxf"; "mxf"; "wm"; "wmv"; "asf"; "dvr-ms"; "dvr"; "wtv"; "dv"; "hdv"; "flv"
        "f4v"; "qt"; "mov"; "hdmov"; "rm"; "rmvb"; "3gpp"; "3gp"; "3gp2"; "3g2"
    ]

    let AudioExtensions = [
        "ac3"; "a52"; "eac3"; "mlp"; "dts"; "dts-hd"; "dtshd"; "true-hd"; "thd"; "truehd"; "thd+ac3"; "tta"; "pcm"
        "wav"; "aiff"; "aif"; "aifc"; "amr"; "awb"; "au"; "snd"; "lpcm"; "ape"; "wv"; "shn"; "adts"
        "adt"; "mpa"; "m1a"; "m2a"; "mp1"; "mp2"; "mp3"; "m4a"; "aac"; "flac"; "oga"; "ogg"; "opus"
        "spx"; "mka"; "weba"; "wma"; "f4a"; "ra"; "ram"; "3ga"; "3ga2"; "ay"; "gbs"; "gym"; "hes"
        "kss"; "nsf"; "nsfe"; "sap"; "spc"; "vgm"; "vgz"; "m3u"; "m3u8"; "pls"; "cue"
    ]

    let ImageExtensions = [ "jpg"; "bmp"; "png"; "gif"; "webp" ]
    let SubtitleExtensions = [ "srt"; "ass"; "idx"; "sub"; "sup"; "ttxt"; "txt"; "ssa"; "smi"; "mks" ]
    let IsoExtensions = [ "iso" ]

    let AllMediaExtensions = 
        VideoExtensions @ AudioExtensions @ ImageExtensions
        |> fun l -> HashSet<string>(l, StringComparer.OrdinalIgnoreCase)

    let isMediaFile (path: string) =
        let ext = Path.GetExtension(path).TrimStart('.')
        AllMediaExtensions.Contains(ext)

    let isSubtitleFile (path: string) =
        let ext = Path.GetExtension(path).TrimStart('.')
        SubtitleExtensions |> List.exists (fun e -> e.Equals(ext, StringComparison.OrdinalIgnoreCase))

    let isIsoFile (path: string) =
        let ext = Path.GetExtension(path).TrimStart('.')
        IsoExtensions |> List.exists (fun e -> e.Equals(ext, StringComparison.OrdinalIgnoreCase))

    let isDiscDirectory (path: string) =
        Directory.Exists(path) &&
        (Directory.Exists(Path.Combine(path, "BDMV")) ||
         Directory.EnumerateFiles(path, "*.ifo", SearchOption.TopDirectoryOnly).Any())

    let getDiscKindForDirectory (path: string) =
        if Directory.Exists(Path.Combine(path, "BDMV")) then Bluray else Dvd

    let getDiscKindForIso (path: string) =
        let info = FileInfo(path)
        let sizeGb = float info.Length / 1e9
        if sizeGb > 4.7 then Bluray else Dvd

    let naturalCompare (s1: string) (s2: string) =
        if isNull s1 && isNull s2 then 0
        elif isNull s1 then -1
        elif isNull s2 then 1
        else
            let splitRegex = Regex("([0-9]+)", RegexOptions.IgnoreCase)
            let parts1 = splitRegex.Split(s1.ToLowerInvariant()) |> Array.filter (fun x -> not (String.IsNullOrEmpty(x)))
            let parts2 = splitRegex.Split(s2.ToLowerInvariant()) |> Array.filter (fun x -> not (String.IsNullOrEmpty(x)))
            
            let length = Math.Max(parts1.Length, parts2.Length)
            let mutable result = 0
            let mutable i = 0
            while i < length && result = 0 do
                if i >= parts1.Length then result <- -1
                elif i >= parts2.Length then result <- 1
                else
                    let p1 = parts1.[i]
                    let p2 = parts2.[i]
                    match Int64.TryParse(p1), Int64.TryParse(p2) with
                    | (true, n1), (true, n2) -> result <- n1.CompareTo(n2)
                    | _ -> result <- String.Compare(p1, p2, StringComparison.Ordinal)
                i <- i + 1
            result
