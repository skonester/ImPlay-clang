namespace ImPlay.Core;

public static class PathHelper
{
    public static string GetConfigDir()
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var portableDir = Path.Combine(exeDir, "portable_config");
        
        if (Directory.Exists(portableDir))
        {
            return portableDir;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = Path.Combine(appData, "ImPlay");
        Directory.CreateDirectory(path);
        return path;
    }
}
