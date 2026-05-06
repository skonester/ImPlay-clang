namespace ImPlay.Core.Services

open System
open System.Collections.Generic
open System.IO
open System.IO.Compression
open System.Linq
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open System.Text.Json
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks
open ImPlay.Core.Models

type SubtitleSearchService() =
    let ProviderTimeout = TimeSpan.FromSeconds(6.0)
    
    let createClient() =
        let client = new HttpClient(Timeout = TimeSpan.FromSeconds(20.0))
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TemporaryUserAgent") |> ignore
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-User-Agent", "TemporaryUserAgent") |> ignore
        client.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/json"))
        client

    let http = createClient()

    let getStr (el: JsonElement) (name: string) =
        match el.TryGetProperty(name) with
        | true, p -> p.GetString()
        | _ -> null

    let normalizeQuery (query: string) =
        let stem = Path.GetFileNameWithoutExtension(query.Trim())
        let mutable n = Regex.Replace(stem, @"\[[^\]]*\]|\([^\)]*\)", " ")
        n <- Regex.Replace(n, @"[._]+", " ")
        n <- Regex.Replace(n, @"\s+", " ").Trim()
        n <- Regex.Replace(n, @"\b(480p|576p|720p|1080p|2160p|4k|uhd|hdr|dv|bluray|brrip|bdrip|web\s?dl|webrip|hdtv|xvid|x264|x265|h\s?264|h\s?265|hevc|aac|ac3|dts|ddp?5\s?1|10bit)\b.*$", "", RegexOptions.IgnoreCase).Trim()
        Regex.Replace(n, @"\s+", " ")

    let sanitizeFileName (name: string) =
        let invalid = Path.GetInvalidFileNameChars()
        let mutable sanitized = new string(name |> Seq.map (fun c -> if invalid.Contains(c) then '_' else c) |> Seq.toArray)
        sanitized <- sanitized.Trim()
        if sanitized.Length <= 120 then sanitized
        else
            let ext = Path.GetExtension(sanitized)
            let stem = Path.GetFileNameWithoutExtension(sanitized)
            let maxStem = Math.Max(1, 120 - ext.Length)
            stem.Substring(0, Math.Min(stem.Length, maxStem)) + ext

    let stripTags (html: string) =
        let mutable text = Regex.Replace(html, @"<br\s*/?>", " ", RegexOptions.IgnoreCase)
        text <- Regex.Replace(text, "<.*?>", " ")
        text <- WebUtility.HtmlDecode(text)
        Regex.Replace(text, @"\s+", " ").Trim()

    let toTitleCase (text: string) =
        String.Join(" ", text.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries) |> Seq.map (fun w -> Char.ToUpperInvariant(w.[0]).ToString() + w.Substring(1).ToLowerInvariant()))

    let getYtsLanguageSlug (osLangCode: string) =
        match osLangCode with
        | "eng" -> Some "english"
        | "spa" -> Some "spanish"
        | "fre" -> Some "french"
        | "ger" -> Some "german"
        | "ita" -> Some "italian"
        | "por" -> Some "portuguese"
        | "dut" -> Some "dutch"
        | "ara" -> Some "arabic"
        | "chi" -> Some "chinese"
        | "jpn" -> Some "japanese"
        | "kor" -> Some "korean"
        | "rus" -> Some "russian"
        | _ -> None

    let parseOpenSubtitles (json: string) =
        if String.IsNullOrWhiteSpace(json) || not (json.TrimStart().StartsWith("[")) then []
        else
            try
                use doc = JsonDocument.Parse(json)
                let results = ResizeArray<SubtitleSearchResult>()
                for item in doc.RootElement.EnumerateArray() do
                    let dl = getStr item "SubDownloadLink"
                    if not (String.IsNullOrWhiteSpace(dl)) then
                        results.Add({
                            Source = "OpenSubtitles"
                            Title = getStr item "MovieName" |> Option.ofObj |> Option.defaultValue ""
                            FileName = getStr item "SubFileName" |> Option.ofObj |> Option.defaultValue "subtitle.srt"
                            Language = getStr item "LanguageName" |> Option.ofObj |> Option.defaultValue (getStr item "SubLanguageID" |> Option.ofObj |> Option.defaultValue "")
                            Format = (getStr item "SubFormat" |> Option.ofObj |> Option.defaultValue "srt").ToUpperInvariant()
                            DownloadUrl = dl
                            Downloads = match Int32.TryParse(getStr item "SubDownloadsCnt") with (true, d) -> d | _ -> 0
                        })
                results |> Seq.take (Math.Min(results.Count, 60)) |> Seq.toList
            with _ -> []

    let parsePodnapisi (json: string) =
        if String.IsNullOrWhiteSpace(json) then []
        else
            try
                use doc = JsonDocument.Parse(json)
                match doc.RootElement.TryGetProperty("results") with
                | true, arr when arr.ValueKind = JsonValueKind.Array ->
                    let results = ResizeArray<SubtitleSearchResult>()
                    for item in arr.EnumerateArray() do
                        let id = getStr item "id"
                        if not (String.IsNullOrWhiteSpace(id)) then
                            let title = getStr item "title" |> Option.ofObj |> Option.defaultValue ""
                            let lang = getStr item "language" |> Option.ofObj |> Option.defaultValue ""
                            let fmt = (getStr item "format" |> Option.ofObj |> Option.defaultValue "srt").ToUpperInvariant()
                            let dlUrl = $"https://www.podnapisi.net/subtitles/{id}/download"
                            let mutable dlCount = 0
                            match item.TryGetProperty("downloads") with
                            | true, dlp when dlp.ValueKind = JsonValueKind.Number -> dlp.TryGetInt32(&dlCount) |> ignore
                            | _ -> ()
                            
                            let mutable releaseName = title
                            match item.TryGetProperty("releases") with
                            | true, relArr when relArr.ValueKind = JsonValueKind.Array ->
                                let first = relArr.EnumerateArray().FirstOrDefault().GetString()
                                if not (String.IsNullOrWhiteSpace(first)) then releaseName <- first
                            | _ -> ()
                            
                            results.Add({
                                Source = "Podnapisi"
                                Title = title
                                FileName = $"{sanitizeFileName releaseName}.{fmt.ToLowerInvariant()}"
                                Language = lang
                                Format = fmt
                                DownloadUrl = dlUrl
                                Downloads = dlCount
                            })
                    results |> Seq.take (Math.Min(results.Count, 30)) |> Seq.toList
                | _ -> []
            with _ -> []

    let parseYtsSubs (html: string) (langSlug: string) =
        if String.IsNullOrWhiteSpace(html) then []
        else
            let rows = Regex.Matches(html, @"<tr\b[^>]*>(?<row>.*?)</tr>", RegexOptions.IgnoreCase ||| RegexOptions.Singleline)
            let results = ResizeArray<SubtitleSearchResult>()
            for m in rows do
                let row = m.Groups.["row"].Value
                let langMatch = Regex.Match(row, @"<span\s+class=""sub-lang"">\s*(?<lang>.*?)\s*</span>", RegexOptions.IgnoreCase ||| RegexOptions.Singleline)
                if langMatch.Success then
                    let langVal = stripTags langMatch.Groups.["lang"].Value
                    if langVal.Trim().ToLowerInvariant().Replace(' ', '-') = langSlug then
                        let linkMatch = Regex.Match(row, @"href=""(?<href>/subtitles/[^""]+)""", RegexOptions.IgnoreCase ||| RegexOptions.Singleline)
                        if linkMatch.Success then
                            let href = WebUtility.HtmlDecode(linkMatch.Groups.["href"].Value)
                            let slug = href.Substring(href.LastIndexOf('/') + 1)
                            let dlUrl = $"https://subtitles.yts-subs.com/subtitles/{slug}.zip"
                            let anchor = Regex.Match(row, $@"<a\s+href=""{Regex.Escape(href)}""[^>]*>(?<title>.*?)</a>", RegexOptions.IgnoreCase ||| RegexOptions.Singleline)
                            let mutable title = if anchor.Success then stripTags anchor.Groups.["title"].Value else slug
                            title <- Regex.Replace(title, @"^\s*subtitle\s+", "", RegexOptions.IgnoreCase).Trim()
                            let mutable rating = 0
                            let rMatch = Regex.Match(row, @"<span\s+class=""label[^""]*"">\s*(?<rating>-?\d+)\s*</span>", RegexOptions.IgnoreCase ||| RegexOptions.Singleline)
                            if rMatch.Success then Int32.TryParse(rMatch.Groups.["rating"].Value, &rating) |> ignore
                            results.Add({
                                Source = "YTS Subtitles"
                                Title = "YTS Subtitles"
                                FileName = $"{sanitizeFileName(if String.IsNullOrWhiteSpace(title) then slug else title)}.srt"
                                Language = toTitleCase(langSlug.Replace('-', ' '))
                                Format = "SRT"
                                DownloadUrl = dlUrl
                                Downloads = Math.Max(0, rating)
                            })
            results |> Seq.groupBy (fun r -> r.DownloadUrl) |> Seq.map (fun (_, g) -> g.First()) |> Seq.take (Math.Min(results.Count, 40)) |> Seq.toList

    let searchOpenSubtitlesAsync (query: string) (langCode: string) (ct: CancellationToken) =
        task {
            try
                let q = query.ToLowerInvariant()
                let encoded = Uri.EscapeDataString(q).Replace("%20", "-")
                let url = $"https://rest.opensubtitles.org/search/query-{encoded}/sublanguageid-{langCode}"
                use req = new HttpRequestMessage(HttpMethod.Get, url)
                req.Headers.TryAddWithoutValidation("X-User-Agent", "TemporaryUserAgent") |> ignore
                let! resp = http.SendAsync(req, ct)
                if resp.IsSuccessStatusCode then
                    let! json = resp.Content.ReadAsStringAsync(ct)
                    return parseOpenSubtitles json :> IEnumerable<SubtitleSearchResult>
                else return Seq.empty
            with _ -> return Seq.empty
        }

    let searchPodnapisiAsync (query: string) (langCode: string) (ct: CancellationToken) =
        task {
            try
                let encoded = Uri.EscapeDataString(query)
                let url = $"https://www.podnapisi.net/subtitles/search/old?keywords={encoded}&language={langCode}&format=json"
                let! resp = http.GetAsync(url, ct)
                if resp.IsSuccessStatusCode then
                    let! json = resp.Content.ReadAsStringAsync(ct)
                    return parsePodnapisi json :> IEnumerable<SubtitleSearchResult>
                else return Seq.empty
            with _ -> return Seq.empty
        }

    let findImdbIdViaOpenSubtitlesAsync (query: string) (langCode: string) (ct: CancellationToken) =
        task {
            let languages = if langCode = "eng" then [| "eng" |] else [| langCode; "eng" |]
            let mutable result = None
            for l in languages do
                if result.IsNone then
                    try
                        let encoded = Uri.EscapeDataString(query.ToLowerInvariant()).Replace("%20", "-")
                        let url = $"https://rest.opensubtitles.org/search/query-{encoded}/sublanguageid-{l}"
                        use req = new HttpRequestMessage(HttpMethod.Get, url)
                        req.Headers.TryAddWithoutValidation("X-User-Agent", "TemporaryUserAgent") |> ignore
                        let! resp = http.SendAsync(req, ct)
                        if resp.IsSuccessStatusCode then
                            let! json = resp.Content.ReadAsStringAsync(ct)
                            use doc = JsonDocument.Parse(json)
                            if doc.RootElement.ValueKind = JsonValueKind.Array then
                                for item in doc.RootElement.EnumerateArray() do
                                    if result.IsNone then
                                        match getStr item "IDMovieImdb" with
                                        | s when not (isNull s) ->
                                            match Int32.TryParse(s) with
                                            | true, id when id > 0 -> result <- Some $"tt{id:D7}"
                                            | _ -> ()
                                        | _ -> ()
                    with _ -> ()
            return result
        }

    let searchYtsSubsAsync (query: string) (osLangCode: string) (ct: CancellationToken) =
        task {
            try
                match getYtsLanguageSlug osLangCode with
                | Some slug ->
                    let imdbIdMatch = Regex.Match(query, @"tt\d{7,8}", RegexOptions.IgnoreCase)
                    let! imdbId = 
                        if imdbIdMatch.Success then Task.FromResult(Some (imdbIdMatch.Value.ToLowerInvariant()))
                        else findImdbIdViaOpenSubtitlesAsync query osLangCode ct
                    
                    match imdbId with
                    | Some id ->
                        let! html = http.GetStringAsync($"https://yts-subs.com/movie-imdb/{id}", ct)
                        return parseYtsSubs html slug :> IEnumerable<SubtitleSearchResult>
                    | None -> return Seq.empty
                | None -> return Seq.empty
            with _ -> return Seq.empty
        }

    let searchProviderWithTimeoutAsync (search: CancellationToken -> Task<IEnumerable<SubtitleSearchResult>>) (ct: CancellationToken) =
        task {
            use timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct)
            let t = search timeoutCts.Token
            try
                return! t.WaitAsync(ProviderTimeout, ct)
            with
            | :? TimeoutException ->
                timeoutCts.Cancel()
                return Seq.empty
            | _ -> return Seq.empty
        }

    let extractGzipAsync (bytes: byte[]) (destPath: string) (ct: CancellationToken) =
        task {
            try
                use ms = new MemoryStream(bytes)
                use gz = new GZipStream(ms, CompressionMode.Decompress)
                use fs = File.Create(destPath)
                do! gz.CopyToAsync(fs, ct)
            with :? InvalidDataException ->
                do! File.WriteAllBytesAsync(destPath, bytes, ct)
        }

    let extractZipAsync (bytes: byte[]) (destPath: string) (ct: CancellationToken) =
        task {
            use ms = new MemoryStream(bytes)
            use zip = new ZipArchive(ms, ZipArchiveMode.Read)
            let entry = 
                zip.Entries |> Seq.tryFind (fun e -> 
                    e.Name.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.EndsWith(".ass", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.EndsWith(".ssa", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.EndsWith(".sub", StringComparison.OrdinalIgnoreCase))
            
            match entry with
            | Some e ->
                use fs = File.Create(destPath)
                use es = e.Open()
                do! es.CopyToAsync(fs, ct)
            | None -> raise (InvalidDataException("No subtitle file found inside the downloaded archive."))
        }

    member _.Languages = [
        ("English", "eng", "en")
        ("Spanish", "spa", "es")
        ("French", "fre", "fr")
        ("German", "ger", "de")
        ("Italian", "ita", "it")
        ("Portuguese", "por", "pt")
        ("Dutch", "dut", "nl")
        ("Arabic", "ara", "ar")
        ("Chinese", "chi", "zh")
        ("Japanese", "jpn", "ja")
        ("Korean", "kor", "ko")
        ("Russian", "rus", "ru")
    ]

    member _.SearchAsync(query: string, osLangCode: string, pnLangCode: string, ct: CancellationToken) =
        task {
            let normalized = normalizeQuery query
            if String.IsNullOrWhiteSpace(normalized) then return [||] :> IReadOnlyList<SubtitleSearchResult>
            else
                let tasks = [|
                    searchProviderWithTimeoutAsync (fun t -> searchOpenSubtitlesAsync normalized osLangCode t) ct
                    searchProviderWithTimeoutAsync (fun t -> searchPodnapisiAsync normalized pnLangCode t) ct
                    searchProviderWithTimeoutAsync (fun t -> searchYtsSubsAsync normalized osLangCode t) ct
                |]
                let! batches = Task.WhenAll(tasks)
                let merged = 
                    batches 
                    |> Seq.collect id 
                    |> Seq.groupBy (fun r -> r.DownloadUrl) 
                    |> Seq.map (fun (_, g) -> g.First()) 
                    |> Seq.sortByDescending (fun r -> r.Downloads) 
                    |> Seq.toArray
                return merged :> IReadOnlyList<SubtitleSearchResult>
        }

    member _.DownloadAsync(result: SubtitleSearchResult, ct: CancellationToken) =
        task {
            let tempDir = Path.Combine(Path.GetTempPath(), "ImPlay", "subtitles")
            Directory.CreateDirectory(tempDir) |> ignore
            
            let! bytes = http.GetByteArrayAsync(result.DownloadUrl, ct)
            let mutable safeFile = sanitizeFileName result.FileName
            if String.IsNullOrWhiteSpace(Path.GetExtension(safeFile)) then
                safeFile <- safeFile + "." + result.Format.ToLowerInvariant()
            let destPath = Path.Combine(tempDir, safeFile)
            
            match result.Source with
            | "OpenSubtitles" -> do! extractGzipAsync bytes destPath ct
            | "Podnapisi" | "YTS Subtitles" -> do! extractZipAsync bytes destPath ct
            | _ -> do! File.WriteAllBytesAsync(destPath, bytes, ct)
            
            return destPath
        }
