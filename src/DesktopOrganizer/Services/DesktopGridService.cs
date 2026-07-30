using System.Diagnostics;
using DesktopOrganizer.Interop;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-010 item 8: keeps the desktop's "Align icons to grid" and "Auto arrange icons" turned OFF
/// so the shell stops re-quantizing / repacking the coordinates we send. This is the decisive
/// fix for the container-move scatter; the grid-aligned layout math in
/// <see cref="IconSortService.ComputeGrid"/> is the independent second line of defence.
///
/// Policy only — all raw Win32 lives in <see cref="DesktopViewSettingsInterop"/>, which this
/// class reaches through <c>protected virtual</c> seams so tests never touch P/Invoke.
/// Depends on Interop only, never on UI (Core → UI one-way rule).
///
/// Rules enforced here:
///  - Read state FIRST. If it is unknown, send nothing — the shell commands are toggles, so a
///    blind send could switch a setting ON.
///  - Send only for settings that are actually on, then re-read to VERIFY.
///  - Always log before/after: this changes user OS state and must be auditable.
///  - Never restore on exit: re-enabling align-to-grid would immediately re-snap everything.
/// </summary>
public class DesktopGridService
{
    /// <summary>Minimum gap between state re-reads, so a burst of writes causes one read.</summary>
    private const int RecheckThrottleMs = 1000;

    private readonly SettingsService _settings;
    private readonly Stopwatch _sinceLastCheck = Stopwatch.StartNew();
    private bool _checkedOnce;

    public DesktopGridService(SettingsService settings) => _settings = settings;

    private bool OptedIn => _settings.Config.Settings.DisableDesktopIconGridSettings;

    /// <summary>
    /// Ensures both settings are off. Cheap and safe to call before every position write.
    /// <paramref name="force"/> bypasses the throttle (use at startup and when settings change).
    /// Returns true when both settings are verified off.
    /// </summary>
    public bool EnsureDisabled(bool force = false)
    {
        if (!force && _checkedOnce && _sinceLastCheck.ElapsedMilliseconds < RecheckThrottleMs)
            return true;   // recently checked — assume still clean

        _checkedOnce = true;
        _sinceLastCheck.Restart();

        var before = ReadState();
        if (before is null)
        {
            LogService.Instance.Warn("F-010",
                "Desktop view state unreadable (window missing, explorer busy, or blocked) — " +
                "sending nothing; relying on grid-aligned layout instead.");
            return false;
        }

        var state = before.Value;

        if (!state.SnapToGrid && !state.AutoArrange)
            return true;   // already clean, nothing to log or change

        if (!OptedIn)
        {
            // Diagnostics only — keeps future bug reports readable without changing user state.
            LogService.Instance.Info("F-010",
                $"Desktop snapToGrid={state.SnapToGrid} autoArrange={state.AutoArrange}, " +
                "but auto-disable is turned off in Settings — icons may be repositioned by Windows.");
            return false;
        }

        if (state.AutoArrange) TurnOffAutoArrange();
        if (state.SnapToGrid)  TurnOffSnapToGrid();

        var after = ReadState();
        LogService.Instance.Info("F-010",
            $"Desktop view settings: snapToGrid {state.SnapToGrid}->{after?.SnapToGrid.ToString() ?? "?"}, " +
            $"autoArrange {state.AutoArrange}->{after?.AutoArrange.ToString() ?? "?"}");

        if (after is { SnapToGrid: false, AutoArrange: false }) return true;

        LogService.Instance.Warn("F-010",
            "Could not turn off the desktop grid settings — icon placement may still be " +
            "adjusted by Windows. Grid-aligned layout remains active as a fallback.");
        return false;
    }

    private void TurnOffAutoArrange()
    {
        if (!ToggleAutoArrange())
            LogService.Instance.Warn("F-010", "Auto-arrange toggle command was not delivered.");
    }

    private void TurnOffSnapToGrid()
    {
        if (!ToggleAlignToGrid())
            LogService.Instance.Warn("F-010", "Align-to-grid toggle command was not delivered.");

        // Verify and fall back to the documented extended-style clear if the toggle did not take.
        if (ReadState() is { SnapToGrid: true })
        {
            LogService.Instance.Info("F-010",
                "Align-to-grid still set after the shell command — clearing LVS_EX_SNAPTOGRID directly.");
            ClearSnapToGridStyle();
        }
    }

    // ── Interop seams (overridden in tests so no P/Invoke runs) ───

    protected virtual DesktopViewState? ReadState()      => DesktopViewSettingsInterop.TryReadState();
    protected virtual bool ToggleAutoArrange()          => DesktopViewSettingsInterop.ToggleAutoArrange();
    protected virtual bool ToggleAlignToGrid()          => DesktopViewSettingsInterop.ToggleAlignToGrid();
    protected virtual bool ClearSnapToGridStyle()       => DesktopViewSettingsInterop.ClearSnapToGridExtendedStyle();
}
