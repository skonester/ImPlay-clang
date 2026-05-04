using System.Text;

namespace ImPlay.Core.Services;

public static class StartupLogger
{
    private static readonly object LockObj = new();
    private static string? _logPath;

    public static string LogPath => _logPath ??= Initialize();

    public static string Initialize()
    {
        lock (LockObj)
        {
            var configDir = PathHelper.GetConfigDir();
            var path = Path.Combine(configDir, "startup.log");
            _logPath = path;

            var header = new StringBuilder();
            header.AppendLine(new string('-', 72));
            header.AppendLine($"=== ImPlay startup {DateTimeOffset.Now:O} ===");
            header.AppendLine($"BaseDir={AppDomain.CurrentDomain.BaseDirectory}");
            header.AppendLine();

            File.AppendAllText(path, header.ToString(), Encoding.UTF8);
            return path;
        }
    }

    public static void Log(string message)
    {
        lock (LockObj)
        {
            var line = $"[{DateTimeOffset.Now:O}] {message}\n";
            try
            {
                File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
            catch
            {
                // Ignore logging failures
            }
        }
    }

    public static void LogException(string context, Exception ex)
    {
        Log($"{context} failed: {ex.Message}\n{ex.StackTrace}");
    }
}
