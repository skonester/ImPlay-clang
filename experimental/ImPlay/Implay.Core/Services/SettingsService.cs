using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ImPlay.Core.Services;

public sealed class SettingsService
{
    private const int MaxRecentFiles = 12;

    private readonly string _settingsPath;
    private readonly Dictionary<string, double> _resumePositions;
    private readonly Dictionary<string, double> _resumeDurations;
    private readonly List<string> _recentFiles;
    private readonly Dictionary<string, SubtitleEntry> _subtitleSettings;
    private readonly Dictionary<string, List<BookmarkEntry>> _bookmarks;

    // ── Session-level preferences ─────────────────────────────────────────
    public int   LastVolume   { get; private set; } = 80;
    public float LastSpeed    { get; private set; } = 1.0f;
    public int   SeekStep     { get; private set; } = 5;    // seconds (5 | 10 | 30)
    public VideoRendererKind VideoRenderer { get; private set; } = VideoRendererKind.NativeVulkan;

    public SettingsService()
    {
        var configDir = PathHelper.GetConfigDir();
        _settingsPath = Path.Combine(configDir, "settings.json");

        var settings = LoadSettings();
        _resumePositions  = settings.ResumePositions;
        _resumeDurations  = settings.ResumeDurations;
        _recentFiles      = settings.RecentFiles;
        _subtitleSettings = settings.SubtitleSettings;
        _bookmarks        = settings.Bookmarks;
        LastVolume        = settings.LastVolume;
        LastSpeed         = settings.LastSpeed;
        SeekStep          = settings.SeekStep;
        VideoRenderer     = settings.VideoRenderer;
    }

    public IReadOnlyList<string> RecentFiles => _recentFiles.AsReadOnly();

    // ── Recent files ────────────────────────────────────────────────────────

    public void SaveSessionPreferences(int volume, float speed, int seekStep)
    {
        LastVolume = Math.Clamp(volume, 0, 150);
        LastSpeed  = Math.Clamp(speed, 0.25f, 4.0f);
        SeekStep   = seekStep is 5 or 10 or 30 ? seekStep : 5;
        Save();
    }

    public void SaveVideoRenderer(VideoRendererKind renderer)
    {
        VideoRenderer = renderer;
        Save();
    }

    // ── Recent files ────────────────────────────────────────────────────────

    public void AddRecentFile(string filePath)
    {
        _recentFiles.Remove(filePath);
        _recentFiles.Insert(0, filePath);
        if (_recentFiles.Count > MaxRecentFiles)
            _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);
        Save();
    }

    public void RemoveRecentFile(string filePath)
    {
        if (_recentFiles.Remove(filePath))
            Save();
    }

    // ── Resume positions ────────────────────────────────────────────────────

    public TimeSpan GetResumePosition(string filePath)
    {
        return _resumePositions.TryGetValue(KeyForFile(filePath), out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;
    }

    public void SaveResumePosition(string? filePath, TimeSpan position, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var key = KeyForFile(filePath);
        if (position.TotalSeconds < 5 || (duration > TimeSpan.Zero && duration - position < TimeSpan.FromSeconds(5)))
        {
            _resumePositions.Remove(key);
            _resumeDurations.Remove(key);
        }
        else
        {
            _resumePositions[key] = position.TotalSeconds;
            if (duration > TimeSpan.Zero)
                _resumeDurations[key] = duration.TotalSeconds;
        }

        Save();
    }

    /// <summary>Returns resume position and progress 0–100, or (Zero, -1) if none saved.</summary>
    public (TimeSpan Position, double ProgressPct) GetResumeInfo(string filePath)
    {
        var key = KeyForFile(filePath);
        if (!_resumePositions.TryGetValue(key, out var pos)) return (TimeSpan.Zero, -1);
        double pct = -1;
        if (_resumeDurations.TryGetValue(key, out var dur) && dur > 0)
            pct = Math.Clamp(pos / dur * 100.0, 0, 100);
        return (TimeSpan.FromSeconds(pos), pct);
    }

    public void ClearResumePosition(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        _resumePositions.Remove(KeyForFile(filePath));
        Save();
    }

    // ── Bookmarks ────────────────────────────────────────────────────────────

    public IReadOnlyList<BookmarkEntry> GetBookmarks(string filePath)
    {
        _bookmarks.TryGetValue(KeyForFile(filePath), out var list);
        return (list ?? []).AsReadOnly();
    }

    public void AddBookmark(string filePath, TimeSpan position, string label)
    {
        var key = KeyForFile(filePath);
        if (!_bookmarks.TryGetValue(key, out var list))
            _bookmarks[key] = list = [];
        list.Add(new BookmarkEntry { PositionSeconds = position.TotalSeconds, Label = label });
        list.Sort((a, b) => a.PositionSeconds.CompareTo(b.PositionSeconds));
        Save();
    }

    public void RemoveBookmark(string filePath, int index)
    {
        var key = KeyForFile(filePath);
        if (!_bookmarks.TryGetValue(key, out var list) || index < 0 || index >= list.Count) return;
        list.RemoveAt(index);
        Save();
    }

    public void RenameBookmark(string filePath, int index, string newLabel)
    {
        var key = KeyForFile(filePath);
        if (!_bookmarks.TryGetValue(key, out var list) || index < 0 || index >= list.Count) return;
        var entry = list[index];
        list[index] = new BookmarkEntry { PositionSeconds = entry.PositionSeconds, Label = newLabel };
        Save();
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    private SettingsFile LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return new SettingsFile();
        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<SettingsFile>(json) ?? new SettingsFile();
        }
        catch
        {
            return new SettingsFile();
        }
    }

    private void Save()
    {
        var settings = new SettingsFile
        {
            ResumePositions  = _resumePositions,
            ResumeDurations  = _resumeDurations,
            RecentFiles      = _recentFiles,
            SubtitleSettings = _subtitleSettings,
            Bookmarks        = _bookmarks,
            LastVolume       = LastVolume,
            LastSpeed        = LastSpeed,
            SeekStep         = SeekStep,
            VideoRenderer    = VideoRenderer
        };
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    private static string KeyForFile(string filePath)
    {
        var normalized = Path.GetFullPath(filePath);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ── Subtitle settings per file ───────────────────────────────────────────

    public SubtitleEntry? GetSubtitleSettings(string filePath)
    {
        _subtitleSettings.TryGetValue(KeyForFile(filePath), out var entry);
        return entry;
    }

    public void SaveSubtitleSettings(string filePath, SubtitleEntry entry)
    {
        _subtitleSettings[KeyForFile(filePath)] = entry;
        Save();
    }

    public void ClearSubtitleSettings(string filePath)
    {
        if (_subtitleSettings.Remove(KeyForFile(filePath)))
            Save();
    }

    private sealed class SettingsFile
    {
        public Dictionary<string, double> ResumePositions { get; set; } = [];
        public Dictionary<string, double> ResumeDurations { get; set; } = [];
        public List<string> RecentFiles { get; set; } = [];
        public Dictionary<string, SubtitleEntry> SubtitleSettings { get; set; } = [];
        public Dictionary<string, List<BookmarkEntry>> Bookmarks { get; set; } = [];
        public int   LastVolume { get; set; } = 80;
        public float LastSpeed  { get; set; } = 1.0f;
        public int   SeekStep   { get; set; } = 5;
        public VideoRendererKind VideoRenderer { get; set; } = VideoRendererKind.NativeVulkan;
    }
}

public enum VideoRendererKind
{
    NativeVulkan = 0,
    OpenGl = 1
}

/// <summary>Subtitle configuration cached per media file.</summary>
public sealed class SubtitleEntry
{
    public string? FilePath  { get; set; }
    public string  FontSize  { get; set; } = "Medium";
    public string  Font      { get; set; } = "SansSerif";
    public string  Color     { get; set; } = "White";
    public long    DelayMs   { get; set; }
    public int?    EmbeddedTrackId { get; set; }
}

/// <summary>A user-defined timestamp bookmark for a media file.</summary>
public sealed class BookmarkEntry
{
    public double PositionSeconds { get; set; }
    public string Label           { get; set; } = "";

    public TimeSpan Position => TimeSpan.FromSeconds(PositionSeconds);

    public string FormattedTime
    {
        get
        {
            var t = Position;
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes}:{t.Seconds:D2}";
        }
    }
}
