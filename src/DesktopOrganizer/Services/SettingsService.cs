using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-018 / F-019 (basic): Loads and saves the application configuration to
/// %APPDATA%\DesktopOrganizer\config.json using an atomic write (temp-file swap).
/// </summary>
public class SettingsService
{
    private static readonly string DefaultDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DesktopOrganizer");

    private readonly string _dataDir;
    private readonly string _configPath;

    /// <summary>Production constructor — stores data in %APPDATA%\DesktopOrganizer\.</summary>
    public SettingsService() : this(DefaultDataDir) { }

    /// <summary>Testable constructor — stores data in the supplied directory.</summary>
    public SettingsService(string dataDir)
    {
        _dataDir    = dataDir;
        _configPath = Path.Combine(dataDir, "config.json");
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigFile Config { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(_configPath)) return;
            var json = File.ReadAllText(_configPath, Encoding.UTF8);
            Config = JsonSerializer.Deserialize<ConfigFile>(json, JsonOpts) ?? new();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsService] Load failed: {ex.Message}");
            Config = new();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_dataDir);
            var tmp = _configPath + ".tmp";
            var json = JsonSerializer.Serialize(Config, JsonOpts);
            File.WriteAllText(tmp, json, Encoding.UTF8);
            File.Move(tmp, _configPath, overwrite: true);
            Debug.WriteLine($"[SettingsService] Saved {Config.Containers.Count} containers");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsService] Save failed: {ex.Message}");
        }
    }
}
