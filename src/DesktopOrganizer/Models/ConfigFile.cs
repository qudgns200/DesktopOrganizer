namespace DesktopOrganizer.Models;

/// <summary>Root model serialised to %APPDATA%\DesktopOrganizer\config.json.</summary>
public class ConfigFile
{
    public string Version { get; set; } = "1.0.0";
    public AppSettings Settings { get; set; } = new();
    public List<Container> Containers { get; set; } = new();
    public List<Rule> Rules { get; set; } = new();
}
