namespace DesktopOrganizer.Models;

public enum AppLogLevel
{
    Disabled,
    ErrorOnly,
    Info,
    Debug
}

public class AppSettings
{
    public string Version { get; set; } = "1.0.0";
    public bool WatcherEnabled { get; set; } = true;
    public int WatcherDebounceMs { get; set; } = 500;
    public int IconSpacingPx { get; set; } = 8;
    public int MaxContainers { get; set; } = 50;
    public AppLogLevel LogLevel { get; set; } = AppLogLevel.Info;

    // Paths/names excluded from auto-organize (user-added)
    public List<string> ExcludedPaths { get; set; } = new();

    // CLSID strings for system icons excluded by default
    public List<string> ExcludedClsids { get; set; } = new()
    {
        "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", // This PC
        "{645FF040-5081-101B-9F08-00AA002F954E}", // Recycle Bin
        "{450D8FBA-AD25-11D0-98A8-0800361B1103}", // My Documents
        "{208D2C60-3AEA-1069-A2D7-08002B30309D}", // Network
        "{21EC2020-3AEA-1069-A2DD-08002B30309D}"  // Control Panel
    };
}
