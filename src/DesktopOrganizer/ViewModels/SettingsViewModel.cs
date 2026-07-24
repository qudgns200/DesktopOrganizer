using System.Collections.ObjectModel;
using DesktopOrganizer.Models;
using DesktopOrganizer.Resources;
using DesktopOrganizer.ViewModels.Base;

namespace DesktopOrganizer.ViewModels;

/// <summary>
/// Working-copy VM for the Settings dialog (F-023).
/// Initialised from the live <see cref="AppSettings"/>; call <see cref="Validate"/> then
/// <see cref="ApplyTo"/> on OK. Values are written back into the SAME AppSettings instance
/// (in place) rather than replacing it, so already-injected references (e.g. IconSortService's
/// SettingsService reference) automatically observe the new values without extra plumbing.
/// </summary>
public class SettingsViewModel : ObservableObject
{
    private bool        _watcherEnabled;
    private int         _watcherDebounceMs;
    private int         _iconSpacingPx;
    private int         _maxContainers;
    private AppLogLevel _logLevel;
    private bool        _confirmExternalLinkLaunch;
    private string      _newExcludedPath = string.Empty;

    public SettingsViewModel(AppSettings source)
    {
        _watcherEnabled            = source.WatcherEnabled;
        _watcherDebounceMs         = source.WatcherDebounceMs;
        _iconSpacingPx             = source.IconSpacingPx;
        _maxContainers             = source.MaxContainers;
        _logLevel                  = source.LogLevel;
        _confirmExternalLinkLaunch = source.ConfirmExternalLinkLaunch;
        ExcludedPaths              = new ObservableCollection<string>(source.ExcludedPaths);
    }

    public bool WatcherEnabled
    {
        get => _watcherEnabled;
        set => SetField(ref _watcherEnabled, value);
    }

    public int WatcherDebounceMs
    {
        get => _watcherDebounceMs;
        set => SetField(ref _watcherDebounceMs, value);
    }

    public int IconSpacingPx
    {
        get => _iconSpacingPx;
        set => SetField(ref _iconSpacingPx, value);
    }

    public int MaxContainers
    {
        get => _maxContainers;
        set => SetField(ref _maxContainers, value);
    }

    public AppLogLevel LogLevel
    {
        get => _logLevel;
        set => SetField(ref _logLevel, value);
    }

    /// <summary>All available log levels for ComboBox binding.</summary>
    public IReadOnlyList<AppLogLevel> LogLevelOptions { get; } = Enum.GetValues<AppLogLevel>();

    /// <summary>F-025: confirm before launching a .url shortcut from inside a container.</summary>
    public bool ConfirmExternalLinkLaunch
    {
        get => _confirmExternalLinkLaunch;
        set => SetField(ref _confirmExternalLinkLaunch, value);
    }

    public ObservableCollection<string> ExcludedPaths { get; }

    public string NewExcludedPath
    {
        get => _newExcludedPath;
        set => SetField(ref _newExcludedPath, value);
    }

    /// <summary>
    /// Adds <see cref="NewExcludedPath"/> to the list (trimmed, de-duplicated) and clears the input.
    /// Returns the path that was added, or null if the input was empty (nothing added).
    /// The path is added even if it doesn't currently exist on disk — the caller may warn
    /// the user, but a not-yet-created path is intentionally not blocked (F-023 spec).
    /// </summary>
    public string? AddExcludedPath()
    {
        var path = NewExcludedPath.Trim();
        NewExcludedPath = string.Empty;
        if (path.Length == 0) return null;
        if (!ExcludedPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            ExcludedPaths.Add(path);
        return path;
    }

    public void RemoveExcludedPath(string path) => ExcludedPaths.Remove(path);

    /// <summary>
    /// Validates numeric ranges (F-023 acceptance criteria: out-of-range values are
    /// rejected with a message rather than silently clamped). Returns false with a
    /// Korean error message describing the first violated constraint.
    /// </summary>
    public bool Validate(out string error)
    {
        if (WatcherDebounceMs < 100 || WatcherDebounceMs > 5000)
        {
            error = Strings.Settings_DebounceRangeError;
            return false;
        }
        if (IconSpacingPx < 0 || IconSpacingPx > 64)
        {
            error = Strings.Settings_SpacingRangeError;
            return false;
        }
        if (MaxContainers < 1 || MaxContainers > 200)
        {
            error = Strings.Settings_MaxContainersRangeError;
            return false;
        }
        error = string.Empty;
        return true;
    }

    /// <summary>Writes the validated working values into the live AppSettings object.</summary>
    public void ApplyTo(AppSettings target)
    {
        target.WatcherEnabled            = WatcherEnabled;
        target.WatcherDebounceMs         = WatcherDebounceMs;
        target.IconSpacingPx             = IconSpacingPx;
        target.MaxContainers             = MaxContainers;
        target.LogLevel                  = LogLevel;
        target.ConfirmExternalLinkLaunch = ConfirmExternalLinkLaunch;
        target.ExcludedPaths             = ExcludedPaths.ToList();
    }
}
