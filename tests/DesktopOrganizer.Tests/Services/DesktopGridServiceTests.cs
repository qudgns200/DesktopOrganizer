using DesktopOrganizer.Interop;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

/// <summary>
/// F-010 item 8: DesktopGridService policy. The raw Win32 ops are overridden so no P/Invoke runs.
/// The most important guarantee here is that we NEVER send a toggle command blindly — the shell's
/// View-menu commands flip the setting, so sending one when the state is unknown (or already off)
/// could switch it ON and cause the very scatter we're fixing.
/// </summary>
public class DesktopGridServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _settings;

    public DesktopGridServiceTests()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), $"DOGrid_{Guid.NewGuid():N}");
        _settings = new SettingsService(_tempDir);
        _settings.Load();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private TestableDesktopGridService Sut(DesktopViewState? state) => new(_settings, state);

    // ── Nothing to do ─────────────────────────────────────────────

    [Fact]
    public void EnsureDisabled_BothAlreadyOff_SendsNothing_AndSucceeds()
    {
        var sut = Sut(new DesktopViewState(SnapToGrid: false, AutoArrange: false));

        Assert.True(sut.EnsureDisabled(force: true));
        Assert.Equal(0, sut.AlignToGridToggles);
        Assert.Equal(0, sut.AutoArrangeToggles);
        Assert.Equal(0, sut.StyleClears);
    }

    // ── Unknown state must never trigger a toggle ─────────────────

    [Fact]
    public void EnsureDisabled_StateUnreadable_SendsNothing_AndReportsFailure()
    {
        var sut = Sut(state: null);   // desktop not found / message filtered

        Assert.False(sut.EnsureDisabled(force: true));
        Assert.Equal(0, sut.AlignToGridToggles);
        Assert.Equal(0, sut.AutoArrangeToggles);
        Assert.Equal(0, sut.StyleClears);
    }

    // ── Targeted toggles ──────────────────────────────────────────

    [Fact]
    public void EnsureDisabled_SnapToGridOn_SendsOnlyAlignToGridToggle()
    {
        var sut = Sut(new DesktopViewState(SnapToGrid: true, AutoArrange: false));

        sut.EnsureDisabled(force: true);

        Assert.Equal(1, sut.AlignToGridToggles);
        Assert.Equal(0, sut.AutoArrangeToggles);
    }

    [Fact]
    public void EnsureDisabled_AutoArrangeOn_SendsOnlyAutoArrangeToggle()
    {
        var sut = Sut(new DesktopViewState(SnapToGrid: false, AutoArrange: true));

        sut.EnsureDisabled(force: true);

        Assert.Equal(1, sut.AutoArrangeToggles);
        Assert.Equal(0, sut.AlignToGridToggles);
    }

    [Fact]
    public void EnsureDisabled_BothOn_TogglesBoth_AndSucceedsWhenTheyClear()
    {
        var sut = Sut(new DesktopViewState(SnapToGrid: true, AutoArrange: true));
        sut.ClearStateAfterToggle = true;   // simulate the toggles taking effect

        Assert.True(sut.EnsureDisabled(force: true));
        Assert.Equal(1, sut.AlignToGridToggles);
        Assert.Equal(1, sut.AutoArrangeToggles);
    }

    // ── Fallback path ─────────────────────────────────────────────

    [Fact]
    public void EnsureDisabled_ToggleIneffective_FallsBackToExtendedStyleClear()
    {
        // State never clears → the shell command didn't take → use the documented style clear.
        var sut = Sut(new DesktopViewState(SnapToGrid: true, AutoArrange: false));
        sut.ClearStateAfterToggle = false;

        Assert.False(sut.EnsureDisabled(force: true));
        Assert.Equal(1, sut.AlignToGridToggles);
        Assert.Equal(1, sut.StyleClears);
    }

    // ── Opt-out ───────────────────────────────────────────────────

    [Fact]
    public void EnsureDisabled_OptedOut_ReadsAndLogsButSendsNothing()
    {
        _settings.Config.Settings.DisableDesktopIconGridSettings = false;
        var sut = Sut(new DesktopViewState(SnapToGrid: true, AutoArrange: true));

        Assert.False(sut.EnsureDisabled(force: true));
        Assert.Equal(0, sut.AlignToGridToggles);
        Assert.Equal(0, sut.AutoArrangeToggles);
        Assert.True(sut.ReadCount > 0, "state should still be read for diagnostics");
    }

    // ── Throttling ────────────────────────────────────────────────

    [Fact]
    public void EnsureDisabled_WithinThrottleWindow_DoesNotReReadState()
    {
        var sut = Sut(new DesktopViewState(SnapToGrid: false, AutoArrange: false));

        sut.EnsureDisabled(force: true);
        int readsAfterFirst = sut.ReadCount;
        sut.EnsureDisabled();   // immediately again, not forced

        Assert.Equal(readsAfterFirst, sut.ReadCount);
    }

    [Fact]
    public void EnsureDisabled_Force_BypassesThrottle()
    {
        var sut = Sut(new DesktopViewState(SnapToGrid: false, AutoArrange: false));

        sut.EnsureDisabled(force: true);
        int readsAfterFirst = sut.ReadCount;
        sut.EnsureDisabled(force: true);

        Assert.True(sut.ReadCount > readsAfterFirst);
    }
}

// ── Test double ───────────────────────────────────────────────────────────

/// <summary>Records the raw ops instead of sending any window message.</summary>
internal sealed class TestableDesktopGridService : DesktopGridService
{
    private readonly DesktopViewState? _initialState;

    public TestableDesktopGridService(SettingsService settings, DesktopViewState? state)
        : base(settings) => _initialState = state;

    /// <summary>When true, reads after a toggle report both settings as off.</summary>
    public bool ClearStateAfterToggle { get; set; } = true;

    public int ReadCount          { get; private set; }
    public int AlignToGridToggles { get; private set; }
    public int AutoArrangeToggles { get; private set; }
    public int StyleClears        { get; private set; }

    private bool _toggled;

    protected override DesktopViewState? ReadState()
    {
        ReadCount++;
        if (_initialState is null) return null;
        if (_toggled && ClearStateAfterToggle) return new DesktopViewState(false, false);
        return _initialState;
    }

    protected override bool ToggleAlignToGrid() { AlignToGridToggles++; _toggled = true; return true; }
    protected override bool ToggleAutoArrange() { AutoArrangeToggles++; _toggled = true; return true; }
    protected override bool ClearSnapToGridStyle() { StyleClears++; return true; }
}
