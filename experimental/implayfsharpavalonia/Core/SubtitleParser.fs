namespace ImPlay.Core.Services

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Collections.Generic
open ImPlay.Core.Models

type SubtitleLine = { Start : TimeSpan; End : TimeSpan; Text : string }

module SubtitleParser =
    // Matches: 00:01:23,456 --> 00:01:27,890
    let srtTimecode = Regex(@"(\d+):(\d+):(\d+)[,\.](\d+)\s*-->\s*(\d+):(\d+):(\d+)[,\.](\d+)", RegexOptions.Compiled)
    // Matches the Dialogue line: Layer,Start,End,Style,Name,MarginL,R,V,Effect,Text
    let assDialogue = Regex(@"^Dialogue:\s*\d+,(\d+:\d+:\d+\.\d+),(\d+:\d+:\d+\.\d+),[^,]*,[^,]*,[^,]*,[^,]*,[^,]*,[^,]*,(.*)$", RegexOptions.Compiled)
    // Strips ASS inline override tags
    let assInlineTags = Regex(@"\{[^}]*\}", RegexOptions.Compiled)
    // Strips HTML-like tags
    let htmlTags = Regex(@"<[^>]+>", RegexOptions.Compiled)

    let private parseSrtTime (m: Match) (g: int) =
        TimeSpan(0, 
            int m.Groups.[g].Value, 
            int m.Groups.[g + 1].Value, 
            int m.Groups.[g + 2].Value, 
            int m.Groups.[g + 3].Value)

    let private tryParseAssTime (s: string) =
        let parts = s.Split(':')
        if parts.Length <> 3 then None
        else
            match Int32.TryParse(parts.[0]), Int32.TryParse(parts.[1]) with
            | (true, h), (true, m) ->
                let secParts = parts.[2].Split('.')
                match Int32.TryParse(secParts.[0]) with
                | (true, s) ->
                    let cs = if secParts.Length > 1 then match Int32.TryParse(secParts.[1]) with (true, x) -> x * 10 | _ -> 0 else 0
                    Some (TimeSpan(0, h, m, s, cs))
                | _ -> None
            | _ -> None

    let private parseSrt (lines: string[]) =
        let result = ResizeArray<SubtitleLine>()
        let mutable i = 0
        while i < lines.Length do
            while i < lines.Length && String.IsNullOrWhiteSpace(lines.[i]) do i <- i + 1
            if i < lines.Length then
                if fst (Int32.TryParse(lines.[i].Trim())) then i <- i + 1
                if i < lines.Length then
                    let m = srtTimecode.Match(lines.[i])
                    if not m.Success then i <- i + 1
                    else
                        let start = parseSrtTime m 1
                        let endPos = parseSrtTime m 5
                        i <- i + 1
                        let sb = StringBuilder()
                        while i < lines.Length && not (String.IsNullOrWhiteSpace(lines.[i])) do
                            if sb.Length > 0 then sb.Append('\n') |> ignore
                            sb.Append(htmlTags.Replace(lines.[i], "")) |> ignore
                            i <- i + 1
                        let t = sb.ToString().Trim()
                        if t.Length > 0 then
                            result.Add({ Start = start; End = endPos; Text = t })
        result |> Seq.toList

    let private parseAss (lines: string[]) =
        let result = ResizeArray<SubtitleLine>()
        for line in lines do
            let m = assDialogue.Match(line)
            if m.Success then
                match tryParseAssTime m.Groups.[1].Value, tryParseAssTime m.Groups.[2].Value with
                | Some start, Some endPos ->
                    let raw = m.Groups.[3].Value
                    let t = assInlineTags.Replace(raw, "").Replace(@"\N", "\n").Replace(@"\n", "\n").Trim()
                    if t.Length > 0 then
                        result.Add({ Start = start; End = endPos; Text = t })
                | _ -> ()
        result |> Seq.toList

    let parse (filePath: string) =
        if not (File.Exists(filePath)) then []
        else
            try
                let lines = File.ReadAllLines(filePath)
                let ext = Path.GetExtension(filePath).ToLowerInvariant()
                if ext = ".ass" || ext = ".ssa" then parseAss lines
                else parseSrt lines
            with _ -> []
