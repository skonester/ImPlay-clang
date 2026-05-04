using System.Text.RegularExpressions;

namespace ImPlay.Core.Services;

public static class MediaService
{
    public static readonly IReadOnlyList<string> VideoExtensions = new[]
    {
        "yuv", "y4m", "m2ts", "m2t", "mts", "mtv", "ts", "tsv", "tsa", "tts", "trp", "mpeg", "mpg",
        "mpe", "mpeg2", "m1v", "m2v", "mp2v", "mpv", "mpv2", "mod", "vob", "vro", "evob", "evo", "mpeg4",
        "m4v", "mp4", "mp4v", "mpg4", "h264", "avc", "x264", "264", "hevc", "h265", "x265", "265", "ogv",
        "ogm", "ogx", "mkv", "mk3d", "webm", "avi", "vfw", "divx", "3iv", "xvid", "nut", "flic", "fli",
        "flc", "nsv", "gxf", "mxf", "wm", "wmv", "asf", "dvr-ms", "dvr", "wtv", "dv", "hdv", "flv",
        "f4v", "qt", "mov", "hdmov", "rm", "rmvb", "3gpp", "3gp", "3gp2", "3g2"
    };

    public static readonly IReadOnlyList<string> AudioExtensions = new[]
    {
        "ac3", "a52", "eac3", "mlp", "dts", "dts-hd", "dtshd", "true-hd", "thd", "truehd", "thd+ac3", "tta", "pcm",
        "wav", "aiff", "aif", "aifc", "amr", "awb", "au", "snd", "lpcm", "ape", "wv", "shn", "adts",
        "adt", "mpa", "m1a", "m2a", "mp1", "mp2", "mp3", "m4a", "aac", "flac", "oga", "ogg", "opus",
        "spx", "mka", "weba", "wma", "f4a", "ra", "ram", "3ga", "3ga2", "ay", "gbs", "gym", "hes",
        "kss", "nsf", "nsfe", "sap", "spc", "vgm", "vgz", "m3u", "m3u8", "pls", "cue"
    };

    public static readonly IReadOnlyList<string> ImageExtensions = new[] { "jpg", "bmp", "png", "gif", "webp" };
    public static readonly IReadOnlyList<string> SubtitleExtensions = new[] { "srt", "ass", "idx", "sub", "sup", "ttxt", "txt", "ssa", "smi", "mks" };
    public static readonly IReadOnlyList<string> IsoExtensions = new[] { "iso" };

    public static readonly HashSet<string> AllMediaExtensions = VideoExtensions
        .Concat(AudioExtensions)
        .Concat(ImageExtensions)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsMediaFile(string path) =>
        AllMediaExtensions.Contains(Path.GetExtension(path).TrimStart('.'));

    public static bool IsSubtitleFile(string path) =>
        SubtitleExtensions.Contains(Path.GetExtension(path).TrimStart('.'), StringComparer.OrdinalIgnoreCase);

    public static bool IsIsoFile(string path) =>
        IsoExtensions.Contains(Path.GetExtension(path).TrimStart('.'), StringComparer.OrdinalIgnoreCase);

    public static bool IsDiscDirectory(string path) =>
        Directory.Exists(path) &&
        (Directory.Exists(Path.Combine(path, "BDMV")) ||
         Directory.EnumerateFiles(path, "*.ifo", SearchOption.TopDirectoryOnly).Any());

    public static DiscKind GetDiscKindForDirectory(string path) =>
        Directory.Exists(Path.Combine(path, "BDMV")) ? DiscKind.Bluray : DiscKind.Dvd;

    public static DiscKind GetDiscKindForIso(string path)
    {
        var fileInfo = new FileInfo(path);
        var sizeGb = (double)fileInfo.Length / 1000.0 / 1000.0 / 1000.0;
        return sizeGb > 4.7 ? DiscKind.Bluray : DiscKind.Dvd;
    }

    public static int NaturalCompare(string? s1, string? s2)
    {
        if (s1 == null && s2 == null) return 0;
        if (s1 == null) return -1;
        if (s2 == null) return 1;

        var splitRegex = new Regex("([0-9]+)", RegexOptions.IgnoreCase);
        var parts1 = splitRegex.Split(s1.ToLowerInvariant()).Where(x => !string.IsNullOrEmpty(x)).ToArray();
        var parts2 = splitRegex.Split(s2.ToLowerInvariant()).Where(x => !string.IsNullOrEmpty(x)).ToArray();

        var length = Math.Max(parts1.Length, parts2.Length);
        for (int i = 0; i < length; i++)
        {
            if (i >= parts1.Length) return -1;
            if (i >= parts2.Length) return 1;

            var p1 = parts1[i];
            var p2 = parts2[i];

            bool isNum1 = long.TryParse(p1, out var n1);
            bool isNum2 = long.TryParse(p2, out var n2);

            int res;
            if (isNum1 && isNum2)
                res = n1.CompareTo(n2);
            else
                res = string.Compare(p1, p2, StringComparison.Ordinal);

            if (res != 0) return res;
        }

        return 0;
    }

    public enum DiscKind
    {
        Dvd,
        Bluray
    }
}
