using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-018 / F-019: Loads and saves the application configuration to
/// %APPDATA%\DesktopOrganizer\config.json.
/// - Atomic write (temp-file swap) guards against corruption on crash.
/// - Up to 3 rotating backup files (config.json.bak1~3) are maintained.
/// - On load failure the most recent valid backup is used automatically.
/// </summary>
public class SettingsService
{
    private const int MaxBackups = 3;

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

    // ── F-019: Load ───────────────────────────────────────────────

    /// <summary>
    /// Loads config from disk.  On failure tries up to <see cref="MaxBackups"/> backup files
    /// before falling back to a default <see cref="ConfigFile"/>.
    /// </summary>
    public void Load()
    {
        if (TryLoadFrom(_configPath, out var cfg))
        {
            Config = cfg!;
            return;
        }

        // Main file missing or corrupted — try backups newest-first
        for (int i = 1; i <= MaxBackups; i++)
        {
            var bak = BackupPath(i);
            if (TryLoadFrom(bak, out cfg))
            {
                LogService.Instance.Warn("F-019", $"Restored from backup: {bak}");
                Config = cfg!;
                return;
            }
        }

        LogService.Instance.Warn("F-019", "No valid config found — using defaults");
        Config = new();
    }

    private bool TryLoadFrom(string path, out ConfigFile? result)
    {
        result = null;
        if (!File.Exists(path)) return false;
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            result = JsonSerializer.Deserialize<ConfigFile>(json, JsonOpts) ?? new();
            return true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Warn("F-019", $"Cannot read '{path}': {ex.Message}");
            return false;
        }
    }

    // ── F-018: Save ───────────────────────────────────────────────

    /// <summary>
    /// Atomically saves the current config.  Rotates up to <see cref="MaxBackups"/>
    /// backup files before overwriting the main file.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_dataDir);
            RotateBackups();

            var tmp  = _configPath + ".tmp";
            var json = JsonSerializer.Serialize(Config, JsonOpts);
            File.WriteAllText(tmp, json, Encoding.UTF8);
            File.Move(tmp, _configPath, overwrite: true);

            LogService.Instance.Info("F-018", $"Saved {Config.Containers.Count} containers, {Config.Rules.Count} rules");
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("F-018", $"Save failed: {ex.Message}");
        }
    }

    // ── Backup helpers ────────────────────────────────────────────

    private string BackupPath(int n) => $"{_configPath}.bak{n}";

    /// <summary>
    /// Shifts existing backups: bak(N) ← bak(N-1) ← … ← bak1 ← config.json
    /// The oldest backup (bak3) is discarded.
    /// </summary>
    private void RotateBackups()
    {
        if (!File.Exists(_configPath)) return;

        // Drop the oldest backup to make room
        var oldest = BackupPath(MaxBackups);
        if (File.Exists(oldest)) File.Delete(oldest);

        // Shift bak(N-1) → bak(N)
        for (int i = MaxBackups - 1; i >= 1; i--)
        {
            var src = BackupPath(i);
            var dst = BackupPath(i + 1);
            if (File.Exists(src)) File.Move(src, dst, overwrite: true);
        }

        // Current config → bak1
        File.Copy(_configPath, BackupPath(1), overwrite: true);
    }

    /// <summary>Exposes the layouts subdirectory path for <see cref="LayoutService"/>.</summary>
    public string LayoutsDir => Path.Combine(_dataDir, "layouts");
}
