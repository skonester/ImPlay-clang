using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ImPlay.Core.Models;

namespace ImPlay.Core.Services;

/// <summary>
/// Searches for subtitles on free, no-key-required providers and downloads results.
/// <list type="bullet">
///   <item><b>OpenSubtitles REST</b> – rest.opensubtitles.org (X-User-Agent header, no account)</item>
///   <item><b>Podnapisi</b> – www.podnapisi.net public search API (no auth)</item>
///   <item><b>YTS Subtitles</b> – yts-subs.com pages + subtitles.yts-subs.com ZIP downloads (no account)</item>
/// </list>
/// </summary>
public sealed class SubtitleSearchService
{
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(6);
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TemporaryUserAgent");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-User-Agent", "TemporaryUserAgent");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    // ── Language code mapping ─────────────────────────────────────────────────

    /// <summary>
    /// Supported languages: (display, OpenSubtitles ISO 639-2, Podnapisi ISO 639-1).
    /// </summary>
    public static readonly IReadOnlyList<(string Display, string OsCode, string PnCode)> Languages =
    [
        ("English",    "eng", "en"),
        ("Spanish",    "spa", "es"),
        ("French",     "fre", "fr"),
        ("German",     "ger", "de"),
        ("Italian",    "ita", "it"),
        ("Portuguese", "por", "pt"),
        ("Dutch",      "dut", "nl"),
        ("Arabic",     "ara", "ar"),
        ("Chinese",    "chi", "zh"),
        ("Japanese",   "jpn", "ja"),
        ("Korean",     "kor", "ko"),
        ("Russian",    "rus", "ru"),
    ];

    // ── Search ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches both providers concurrently and merges results sorted by download count.
    /// </summary>
    public async Task<IReadOnlyList<SubtitleSearchResult>> SearchAsync(
        string query,
        string osLangCode = "eng",
        string pnLangCode = "en",
        CancellationToken ct = default)
    {
        var normalizedQuery = NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery)) return [];

        var tasks = new Task<IEnumerable<SubtitleSearchResult>>[]
        {
            SearchProviderWithTimeoutAsync(token => SearchOpenSubtitlesAsync(normalizedQuery, osLangCode, token), ct),
            SearchProviderWithTimeoutAsync(token => SearchPodnapisiAsync(normalizedQuery, pnLangCode, token), ct),
            SearchProviderWithTimeoutAsync(token => SearchYtsSubsAsync(normalizedQuery, osLangCode, token), ct)
        };

        var batches = await Task.WhenAll(tasks);
        return batches
            .SelectMany(r => r)
            .GroupBy(r => r.DownloadUrl)
            .Select(g => g.First())
            .OrderByDescending(r => r.Downloads)
            .ToList();
    }

    private static async Task<IEnumerable<SubtitleSearchResult>> SearchProviderWithTimeoutAsync(
        Func<CancellationToken, Task<IEnumerable<SubtitleSearchResult>>> search,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var task = search(timeoutCts.Token);

        try
        {
            return await task.WaitAsync(ProviderTimeout, ct);
        }
        catch (TimeoutException)
        {
            await timeoutCts.CancelAsync();
            _ = task.ContinueWith(t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            return [];
        }
    }

    // ── OpenSubtitles REST ────────────────────────────────────────────────────

    private static async Task<IEnumerable<SubtitleSearchResult>> SearchOpenSubtitlesAsync(
        string query, string langCode, CancellationToken ct)
    {
        try
        {
            query = query.ToLowerInvariant();

            // Spaces must be replaced by hyphens in the path segment
            var encoded = Uri.EscapeDataString(query).Replace("%20", "-");
            var url = $"https://rest.opensubtitles.org/search/query-{encoded}/sublanguageid-{langCode}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("X-User-Agent", "TemporaryUserAgent");

            var response = await Http.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseOpenSubtitles(json);
        }
        catch { return []; }
    }

    private static IEnumerable<SubtitleSearchResult> ParseOpenSubtitles(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('[')) return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            var results = new List<SubtitleSearchResult>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var dl = GetStr(item, "SubDownloadLink");
                if (string.IsNullOrWhiteSpace(dl)) continue;

                results.Add(new SubtitleSearchResult(
                    Source:      "OpenSubtitles",
                    Title:       GetStr(item, "MovieName") ?? "",
                    FileName:    GetStr(item, "SubFileName") ?? "subtitle.srt",
                    Language:    GetStr(item, "LanguageName") ?? GetStr(item, "SubLanguageID") ?? "",
                    Format:      (GetStr(item, "SubFormat") ?? "srt").ToUpperInvariant(),
                    DownloadUrl: dl,
                    Downloads:   int.TryParse(GetStr(item, "SubDownloadsCnt"), out var d) ? d : 0
                ));
            }

            return results.Take(60);
        }
        catch { return []; }
    }

    // ── Podnapisi ─────────────────────────────────────────────────────────────

    private static async Task<IEnumerable<SubtitleSearchResult>> SearchPodnapisiAsync(
        string query, string langCode, CancellationToken ct)
    {
        try
        {
            var encoded = Uri.EscapeDataString(query);
            var url = $"https://www.podnapisi.net/subtitles/search/old?keywords={encoded}&language={langCode}&format=json";

            var response = await Http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParsePodnapisi(json);
        }
        catch { return []; }
    }

    private static IEnumerable<SubtitleSearchResult> ParsePodnapisi(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("results", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return [];

            var results = new List<SubtitleSearchResult>();

            foreach (var item in arr.EnumerateArray())
            {
                var id = GetStr(item, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;

                var title   = GetStr(item, "title") ?? "";
                var lang    = GetStr(item, "language") ?? "";
                var fmt     = (GetStr(item, "format") ?? "srt").ToUpperInvariant();
                var dlUrl   = $"https://www.podnapisi.net/subtitles/{id}/download";

                var dlCount = 0;
                if (item.TryGetProperty("downloads", out var dlp) && dlp.ValueKind == JsonValueKind.Number)
                    dlp.TryGetInt32(out dlCount);

                // Build a filename from the first release name or the title
                var releaseName = title;
                if (item.TryGetProperty("releases", out var relArr) && relArr.ValueKind == JsonValueKind.Array)
                {
                    var first = relArr.EnumerateArray().FirstOrDefault().GetString();
                    if (!string.IsNullOrWhiteSpace(first)) releaseName = first;
                }

                results.Add(new SubtitleSearchResult(
                    Source:      "Podnapisi",
                    Title:       title,
                    FileName:    $"{SanitizeFileName(releaseName)}.{fmt.ToLowerInvariant()}",
                    Language:    lang,
                    Format:      fmt,
                    DownloadUrl: dlUrl,
                    Downloads:   dlCount
                ));
            }

            return results.Take(30);
        }
        catch { return []; }
    }

    // ── YTS Subtitles ────────────────────────────────────────────────────────

    private static async Task<IEnumerable<SubtitleSearchResult>> SearchYtsSubsAsync(
        string query, string osLangCode, CancellationToken ct)
    {
        try
        {
            var langSlug = GetYtsLanguageSlug(osLangCode);
            if (langSlug is null) return [];

            var imdbId = TryGetImdbId(query) ?? await FindImdbIdViaOpenSubtitlesAsync(query, osLangCode, ct);
            if (string.IsNullOrWhiteSpace(imdbId)) return [];

            var html = await Http.GetStringAsync($"https://yts-subs.com/movie-imdb/{imdbId}", ct);
            return ParseYtsSubs(html, langSlug);
        }
        catch { return []; }
    }

    private static async Task<string?> FindImdbIdViaOpenSubtitlesAsync(
        string query, string langCode, CancellationToken ct)
    {
        var languages = langCode == "eng" ? ["eng"] : new[] { langCode, "eng" };

        foreach (var language in languages)
        {
            var imdbId = await FindImdbIdViaOpenSubtitlesLanguageAsync(query, language, ct);
            if (!string.IsNullOrWhiteSpace(imdbId)) return imdbId;
        }

        return null;
    }

    private static async Task<string?> FindImdbIdViaOpenSubtitlesLanguageAsync(
        string query, string langCode, CancellationToken ct)
    {
        try
        {
            var encoded = Uri.EscapeDataString(query.ToLowerInvariant()).Replace("%20", "-");
            var url = $"https://rest.opensubtitles.org/search/query-{encoded}/sublanguageid-{langCode}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("X-User-Agent", "TemporaryUserAgent");

            var response = await Http.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var imdb = GetStr(item, "IDMovieImdb");
                if (int.TryParse(imdb, out var id) && id > 0)
                    return $"tt{id:0000000}";
            }
        }
        catch { }

        return null;
    }

    private static IEnumerable<SubtitleSearchResult> ParseYtsSubs(string html, string langSlug)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];

        var rows = Regex.Matches(html, @"<tr\b[^>]*>(?<row>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var results = new List<SubtitleSearchResult>();

        foreach (Match match in rows)
        {
            var row = match.Groups["row"].Value;
            var language = Regex.Match(row, @"<span\s+class=""sub-lang"">\s*(?<lang>.*?)\s*</span>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!language.Success || !IsYtsLanguageMatch(language.Groups["lang"].Value, langSlug)) continue;

            var link = Regex.Match(row, @"href=""(?<href>/subtitles/[^""]+)""",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!link.Success) continue;

            var href = WebUtility.HtmlDecode(link.Groups["href"].Value);
            var slug = href[(href.LastIndexOf('/') + 1)..];
            var downloadUrl = $"https://subtitles.yts-subs.com/subtitles/{slug}.zip";

            var anchor = Regex.Match(row, $@"<a\s+href=""{Regex.Escape(href)}""[^>]*>(?<title>.*?)</a>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var title = anchor.Success ? StripTags(anchor.Groups["title"].Value) : slug;
            title = Regex.Replace(title, @"^\s*subtitle\s+", "", RegexOptions.IgnoreCase).Trim();

            var rating = 0;
            var ratingMatch = Regex.Match(row, @"<span\s+class=""label[^""]*"">\s*(?<rating>-?\d+)\s*</span>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (ratingMatch.Success) int.TryParse(ratingMatch.Groups["rating"].Value, out rating);

            var fileName = $"{SanitizeFileName(string.IsNullOrWhiteSpace(title) ? slug : title)}.srt";
            results.Add(new SubtitleSearchResult(
                Source: "YTS Subtitles",
                Title: "YTS Subtitles",
                FileName: fileName,
                Language: ToTitleCase(langSlug.Replace('-', ' ')),
                Format: "SRT",
                DownloadUrl: downloadUrl,
                Downloads: Math.Max(0, rating)
            ));
        }

        return results
            .GroupBy(r => r.DownloadUrl)
            .Select(g => g.First())
            .Take(40);
    }

    // ── Download ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads the subtitle to a per-session temp directory and returns the local path.
    /// OpenSubtitles delivers gzip-compressed content; Podnapisi delivers a ZIP archive.
    /// </summary>
    public async Task<string> DownloadAsync(SubtitleSearchResult result, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ImPlay", "subtitles");
        Directory.CreateDirectory(tempDir);

        var bytes     = await Http.GetByteArrayAsync(result.DownloadUrl, ct);
        var safeFile  = SanitizeFileName(result.FileName);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(safeFile)))
            safeFile += $".{result.Format.ToLowerInvariant()}";
        var destPath  = Path.Combine(tempDir, safeFile);

        switch (result.Source)
        {
            case "OpenSubtitles":
                await ExtractGzipAsync(bytes, destPath, ct);
                break;

            case "Podnapisi":
                await ExtractZipAsync(bytes, destPath, ct);
                break;

            case "YTS Subtitles":
                await ExtractZipAsync(bytes, destPath, ct);
                break;

            default:
                await File.WriteAllBytesAsync(destPath, bytes, ct);
                break;
        }

        return destPath;
    }

    private static async Task ExtractGzipAsync(byte[] bytes, string destPath, CancellationToken ct)
    {
        try
        {
            using var ms  = new MemoryStream(bytes);
            using var gz  = new GZipStream(ms, CompressionMode.Decompress);
            await using var fs = File.Create(destPath);
            await gz.CopyToAsync(fs, ct);
        }
        catch (InvalidDataException)
        {
            // Content is not gzip — save raw
            await File.WriteAllBytesAsync(destPath, bytes, ct);
        }
    }

    private static async Task ExtractZipAsync(byte[] bytes, string destPath, CancellationToken ct)
    {
        using var ms  = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        // Pick the first recognised subtitle entry
        var entry = zip.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ||
            e.Name.EndsWith(".ass", StringComparison.OrdinalIgnoreCase) ||
            e.Name.EndsWith(".ssa", StringComparison.OrdinalIgnoreCase) ||
            e.Name.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase) ||
            e.Name.EndsWith(".sub", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            throw new InvalidDataException("No subtitle file found inside the downloaded archive.");

        await using var fs = File.Create(destPath);
        await using var es = entry.Open();
        await es.CopyToAsync(fs, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? GetStr(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) ? p.GetString() : null;

    private static string NormalizeQuery(string query)
    {
        var normalized = Path.GetFileNameWithoutExtension(query.Trim());
        normalized = Regex.Replace(normalized, @"\[[^\]]*\]|\([^\)]*\)", " ");
        normalized = Regex.Replace(normalized, @"[._]+", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        normalized = Regex.Replace(normalized,
            @"\b(480p|576p|720p|1080p|2160p|4k|uhd|hdr|dv|bluray|brrip|bdrip|web\s?dl|webrip|hdtv|xvid|x264|x265|h\s?264|h\s?265|hevc|aac|ac3|dts|ddp?5\s?1|10bit)\b.*$",
            "",
            RegexOptions.IgnoreCase).Trim();

        return Regex.Replace(normalized, @"\s+", " ");
    }

    private static string? TryGetImdbId(string query)
    {
        var match = Regex.Match(query, @"tt\d{7,8}", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }

    private static string? GetYtsLanguageSlug(string osLangCode) => osLangCode switch
    {
        "eng" => "english",
        "spa" => "spanish",
        "fre" => "french",
        "ger" => "german",
        "ita" => "italian",
        "por" => "portuguese",
        "dut" => "dutch",
        "ara" => "arabic",
        "chi" => "chinese",
        "jpn" => "japanese",
        "kor" => "korean",
        "rus" => "russian",
        _ => null
    };

    private static bool IsYtsLanguageMatch(string value, string langSlug)
    {
        var normalized = StripTags(value).Trim().ToLowerInvariant().Replace(' ', '-');
        return normalized == langSlug;
    }

    private static string StripTags(string html)
    {
        var text = Regex.Replace(html, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<.*?>", " ");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string ToTitleCase(string text) =>
        string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        if (sanitized.Length <= 120) return sanitized;

        var ext = Path.GetExtension(sanitized);
        var stem = Path.GetFileNameWithoutExtension(sanitized);
        var maxStemLength = Math.Max(1, 120 - ext.Length);
        return stem[..Math.Min(stem.Length, maxStemLength)] + ext;
    }
}
