using System.Text.RegularExpressions;

namespace ImPlay.Core.Services;

/// <summary>A single timed subtitle entry.</summary>
public sealed record SubtitleLine(TimeSpan Start, TimeSpan End, string Text);

/// <summary>
/// Minimal parser for SRT and basic ASS/SSA subtitle files.
/// Only the timing and visible text is extracted — styling is ignored.
/// </summary>
public static class SubtitleParser
{
    // ── SRT ───────────────────────────────────────────────────────────────────
    // Matches: 00:01:23,456 --> 00:01:27,890
    private static readonly Regex SrtTimecode = new(
        @"(\d+):(\d+):(\d+)[,\.](\d+)\s*-->\s*(\d+):(\d+):(\d+)[,\.](\d+)",
        RegexOptions.Compiled);

    // ── ASS / SSA ─────────────────────────────────────────────────────────────
    // Matches the Dialogue line: Layer,Start,End,Style,Name,MarginL,R,V,Effect,Text
    private static readonly Regex AssDialogue = new(
        @"^Dialogue:\s*\d+,(\d+:\d+:\d+\.\d+),(\d+:\d+:\d+\.\d+),[^,]*,[^,]*,[^,]*,[^,]*,[^,]*,[^,]*,(.*)$",
        RegexOptions.Compiled);

    // Strips ASS inline override tags like {\pos(...)}, {\an8}, {\c&H...} etc.
    private static readonly Regex AssInlineTags = new(@"\{[^}]*\}", RegexOptions.Compiled);

    // Strips HTML-like tags left in some SRT files
    private static readonly Regex HtmlTags = new(@"<[^>]+>", RegexOptions.Compiled);

    /// <summary>Parse a subtitle file, auto-detecting SRT vs ASS/SSA.</summary>
    public static List<SubtitleLine> Parse(string filePath)
    {
        if (!File.Exists(filePath)) return [];

        string[] lines;
        try { lines = File.ReadAllLines(filePath); }
        catch { return []; }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".ass" or ".ssa" ? ParseAss(lines) : ParseSrt(lines);
    }

    // ── SRT parser ────────────────────────────────────────────────────────────

    private static List<SubtitleLine> ParseSrt(string[] lines)
    {
        var result = new List<SubtitleLine>();
        int i = 0;

        while (i < lines.Length)
        {
            // Skip blank lines and sequence numbers
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i])) i++;
            if (i >= lines.Length) break;

            // Skip a purely numeric sequence number if present
            if (int.TryParse(lines[i].Trim(), out _)) i++;
            if (i >= lines.Length) break;

            // Timecode line
            var m = SrtTimecode.Match(lines[i]);
            if (!m.Success) { i++; continue; }

            var start = ParseSrtTime(m, 1);
            var end   = ParseSrtTime(m, 5);
            i++;

            // Text lines until blank or EOF
            var text = new System.Text.StringBuilder();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(HtmlTags.Replace(lines[i], ""));
                i++;
            }

            var t = text.ToString().Trim();
            if (t.Length > 0)
                result.Add(new SubtitleLine(start, end, t));
        }

        return result;
    }

    private static TimeSpan ParseSrtTime(Match m, int g) =>
        new(0,
            int.Parse(m.Groups[g].Value),
            int.Parse(m.Groups[g + 1].Value),
            int.Parse(m.Groups[g + 2].Value),
            int.Parse(m.Groups[g + 3].Value));

    // ── ASS / SSA parser ──────────────────────────────────────────────────────

    private static List<SubtitleLine> ParseAss(string[] lines)
    {
        var result = new List<SubtitleLine>();

        foreach (var line in lines)
        {
            var m = AssDialogue.Match(line);
            if (!m.Success) continue;

            if (!TryParseAssTime(m.Groups[1].Value, out var start)) continue;
            if (!TryParseAssTime(m.Groups[2].Value, out var end))   continue;

            var raw  = m.Groups[3].Value;
            // Strip override tags and \N / \n hard line breaks
            var text = AssInlineTags.Replace(raw, "")
                          .Replace(@"\N", "\n")
                          .Replace(@"\n", "\n")
                          .Trim();

            if (text.Length > 0)
                result.Add(new SubtitleLine(start, end, text));
        }

        return result;
    }

    // ASS time format: H:MM:SS.cc  (centiseconds)
    private static bool TryParseAssTime(string s, out TimeSpan t)
    {
        t = default;
        var parts = s.Split(':');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var h)) return false;
        if (!int.TryParse(parts[1], out var min)) return false;
        var secParts = parts[2].Split('.');
        if (!int.TryParse(secParts[0], out var sec)) return false;
        var cs = secParts.Length > 1 && int.TryParse(secParts[1], out var x) ? x * 10 : 0;
        t = new TimeSpan(0, h, min, sec, cs);
        return true;
    }
}
