namespace ImPlay.Core.Models;

/// <summary>A subtitle result returned from an online search provider.</summary>
public sealed record SubtitleSearchResult(
    string Source,       // "OpenSubtitles" | "Podnapisi"
    string Title,        // Movie / show title
    string FileName,     // Subtitle file name (may include extension)
    string Language,     // Human-readable language name
    string Format,       // SRT | ASS | SSA | VTT …
    string DownloadUrl,  // Direct URL used by DownloadAsync
    int    Downloads     // Download count — used for relevance sorting
);
