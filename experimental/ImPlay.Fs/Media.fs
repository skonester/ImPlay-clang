namespace ImPlay.Fs

open System
open System.IO
open Avalonia.Platform.Storage

module Media =

    type DiscKind =
        | Dvd
        | Bluray

    let videoTypes = [
        "yuv"; "y4m"; "m2ts"; "m2t"; "mts"; "mtv"; "ts"; "tsv"; "tsa"; "tts"; "trp"; "mpeg"; "mpg"
        "mpe"; "mpeg2"; "m1v"; "m2v"; "mp2v"; "mpv"; "mpv2"; "mod"; "vob"; "vro"; "evob"; "evo"; "mpeg4"
        "m4v"; "mp4"; "mp4v"; "mpg4"; "h264"; "avc"; "x264"; "264"; "hevc"; "h265"; "x265"; "265"; "ogv"
        "ogm"; "ogx"; "mkv"; "mk3d"; "webm"; "avi"; "vfw"; "divx"; "3iv"; "xvid"; "nut"; "flic"; "fli"
        "flc"; "nsv"; "gxf"; "mxf"; "wm"; "wmv"; "asf"; "dvr-ms"; "dvr"; "wtv"; "dv"; "hdv"; "flv"
        "f4v"; "qt"; "mov"; "hdmov"; "rm"; "rmvb"; "3gpp"; "3gp"; "3gp2"; "3g2"
    ]

    let audioTypes = [
        "ac3"; "a52"; "eac3"; "mlp"; "dts"; "dts-hd"; "dtshd"; "true-hd"; "thd"; "truehd"; "thd+ac3"; "tta"; "pcm"
        "wav"; "aiff"; "aif"; "aifc"; "amr"; "awb"; "au"; "snd"; "lpcm"; "ape"; "wv"; "shn"; "adts"
        "adt"; "mpa"; "m1a"; "m2a"; "mp1"; "mp2"; "mp3"; "m4a"; "aac"; "flac"; "oga"; "ogg"; "opus"
        "spx"; "mka"; "weba"; "wma"; "f4a"; "ra"; "ram"; "3ga"; "3ga2"; "ay"; "gbs"; "gym"; "hes"
        "kss"; "nsf"; "nsfe"; "sap"; "spc"; "vgm"; "vgz"; "m3u"; "m3u8"; "pls"; "cue"
    ]

    let imageTypes = [ "jpg"; "bmp"; "png"; "gif"; "webp" ]

    let subtitleTypes = [ "srt"; "ass"; "idx"; "sub"; "sup"; "ttxt"; "txt"; "ssa"; "smi"; "mks" ]

    let isoTypes = [ "iso" ]

    let mediaTypes = videoTypes @ audioTypes @ imageTypes

    let pickerPatterns extensions =
        extensions |> List.map (fun ext -> "*." + ext)

    let videoFilter = 
        let f = FilePickerFileType("Videos Files")
        f.Patterns <- pickerPatterns videoTypes
        f

    let audioFilter = 
        let f = FilePickerFileType("Audio Files")
        f.Patterns <- pickerPatterns audioTypes
        f

    let imageFilter = 
        let f = FilePickerFileType("Image Files")
        f.Patterns <- pickerPatterns imageTypes
        f

    let subtitleFilter = 
        let f = FilePickerFileType("Subtitle Files")
        f.Patterns <- pickerPatterns subtitleTypes
        f

    let isoFilter = 
        let f = FilePickerFileType("ISO Image Files")
        f.Patterns <- pickerPatterns isoTypes
        f

    let mediaFilters = [ videoFilter; audioFilter; imageFilter ]

    let private normalizeExtension (path: string) =
        Path.GetExtension(path).TrimStart('.').ToLowerInvariant()

    let isMediaFile path =
        let ext = normalizeExtension path
        mediaTypes |> List.exists ((=) ext)

    let isSubtitleFile path =
        let ext = normalizeExtension path
        subtitleTypes |> List.exists ((=) ext)

    let isIsoFile path =
        let ext = normalizeExtension path
        isoTypes |> List.exists ((=) ext)

    let isDiscDirectory path =
        Directory.Exists(path) &&
        (Directory.Exists(Path.Combine(path, "BDMV")) ||
         Directory.EnumerateFiles(path, "*.ifo", SearchOption.TopDirectoryOnly) |> Seq.isEmpty |> not)

    let discKindForDirectory path =
        if Directory.Exists(Path.Combine(path, "BDMV")) then
            Bluray
        else
            Dvd

    let discKindForIso path =
        let fileInfo = FileInfo(path)
        let sizeGb = float fileInfo.Length / 1000.0 / 1000.0 / 1000.0
        if sizeGb > 4.7 then Bluray else Dvd

    let titleFromPath (path: string) =
        let fileName = Path.GetFileName(path)
        if String.IsNullOrWhiteSpace(fileName) then path else fileName

    let naturalCompare (s1: string) (s2: string) =
        let split (s: string) =
            System.Text.RegularExpressions.Regex.Split(s.ToLowerInvariant(), "([0-9]+)")
            |> Array.filter (fun x -> not (String.IsNullOrEmpty(x)))

        let a1 = split s1
        let a2 = split s2
        
        let rec compare (parts1: string[]) (parts2: string[]) i =
            if i >= parts1.Length && i >= parts2.Length then 0
            elif i >= parts1.Length then -1
            elif i >= parts2.Length then 1
            else
                let p1 = parts1.[i]
                let p2 = parts2.[i]
                
                let isNum1, n1 = Int64.TryParse p1
                let isNum2, n2 = Int64.TryParse p2
                
                let res =
                    if isNum1 && isNum2 then n1.CompareTo n2
                    else p1.CompareTo p2
                
                if res <> 0 then res
                else compare parts1 parts2 (i + 1)
        
        compare a1 a2 0

    let naturalSort (items: seq<'a>) (selector: 'a -> string) =
        items 
        |> Seq.sortWith (fun a b -> naturalCompare (selector a) (selector b))
        |> Seq.toList

