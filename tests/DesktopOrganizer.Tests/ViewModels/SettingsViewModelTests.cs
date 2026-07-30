using DesktopOrganizer.Models;
using DesktopOrganizer.ViewModels;
using Xunit;

namespace DesktopOrganizer.Tests.ViewModels;

public class SettingsViewModelTests
{
    private static AppSettings DefaultSettings() => new()
    {
        WatcherEnabled            = true,
        WatcherDebounceMs         = 500,
        IconSpacingPx             = 8,
        MaxContainers             = 50,
        LogLevel                  = AppLogLevel.Info,
        ConfirmExternalLinkLaunch = true,
        DisableDesktopIconGridSettings = true,
        ExcludedPaths             = new List<string> { @"C:\excluded1", @"C:\excluded2" }
    };

    // ── Constructor ───────────────────────────────────────────────

    [Fact]
    public void Constructor_CopiesAllSettingsProperties()
    {
        var src = DefaultSettings();
        var vm  = new SettingsViewModel(src);

        Assert.Equal(src.WatcherEnabled,            vm.WatcherEnabled);
        Assert.Equal(src.WatcherDebounceMs,         vm.WatcherDebounceMs);
        Assert.Equal(src.IconSpacingPx,             vm.IconSpacingPx);
        Assert.Equal(src.MaxContainers,             vm.MaxContainers);
        Assert.Equal(src.LogLevel,                  vm.LogLevel);
        Assert.Equal(src.ConfirmExternalLinkLaunch, vm.ConfirmExternalLinkLaunch);
        Assert.Equal(src.DisableDesktopIconGridSettings, vm.DisableDesktopIconGridSettings);
        Assert.Equal(src.ExcludedPaths,             vm.ExcludedPaths);
    }

    [Fact]
    public void ApplyTo_RoundTripsDisableDesktopIconGridSettings()
    {
        var target = DefaultSettings();
        var vm = new SettingsViewModel(target) { DisableDesktopIconGridSettings = false };

        vm.ApplyTo(target);

        Assert.False(target.DisableDesktopIconGridSettings);
    }

    // ── Validate ──────────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultValues_Passes()
    {
        var vm = new SettingsViewModel(DefaultSettings());
        Assert.True(vm.Validate(out var error));
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(5001)]
    public void Validate_WatcherDebounceMsOutOfRange_Fails(int value)
    {
        var vm = new SettingsViewModel(DefaultSettings()) { WatcherDebounceMs = value };
        Assert.False(vm.Validate(out var error));
        Assert.Contains("디바운싱", error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    public void Validate_IconSpacingPxOutOfRange_Fails(int value)
    {
        var vm = new SettingsViewModel(DefaultSettings()) { IconSpacingPx = value };
        Assert.False(vm.Validate(out var error));
        Assert.Contains("아이콘 간격", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void Validate_MaxContainersOutOfRange_Fails(int value)
    {
        var vm = new SettingsViewModel(DefaultSettings()) { MaxContainers = value };
        Assert.False(vm.Validate(out var error));
        Assert.Contains("Container 수", error);
    }

    // ── ApplyTo ───────────────────────────────────────────────────

    [Fact]
    public void ApplyTo_WritesValuesIntoTarget()
    {
        var target = DefaultSettings();
        var vm = new SettingsViewModel(target)
        {
            WatcherEnabled            = false,
            WatcherDebounceMs         = 1000,
            IconSpacingPx             = 16,
            MaxContainers             = 10,
            LogLevel                  = AppLogLevel.Debug,
            ConfirmExternalLinkLaunch = false
        };
        vm.RemoveExcludedPath(@"C:\excluded1");

        vm.ApplyTo(target);

        Assert.False(target.WatcherEnabled);
        Assert.Equal(1000, target.WatcherDebounceMs);
        Assert.Equal(16, target.IconSpacingPx);
        Assert.Equal(10, target.MaxContainers);
        Assert.Equal(AppLogLevel.Debug, target.LogLevel);
        Assert.False(target.ConfirmExternalLinkLaunch);
        Assert.DoesNotContain(@"C:\excluded1", target.ExcludedPaths);
        Assert.Contains(@"C:\excluded2", target.ExcludedPaths);
    }

    // ── AddExcludedPath / RemoveExcludedPath ──────────────────────

    [Fact]
    public void AddExcludedPath_AddsTrimmedPath_AndClearsInput()
    {
        var vm = new SettingsViewModel(DefaultSettings()) { NewExcludedPath = "  C:\\new-path  " };

        vm.AddExcludedPath();

        Assert.Contains(@"C:\new-path", vm.ExcludedPaths);
        Assert.Equal(string.Empty, vm.NewExcludedPath);
    }

    [Fact]
    public void AddExcludedPath_EmptyInput_DoesNotAdd()
    {
        var vm = new SettingsViewModel(DefaultSettings()) { NewExcludedPath = "   " };

        vm.AddExcludedPath();

        Assert.Equal(2, vm.ExcludedPaths.Count); // unchanged from DefaultSettings
    }

    [Fact]
    public void AddExcludedPath_Duplicate_DoesNotAddTwice()
    {
        var vm = new SettingsViewModel(DefaultSettings()) { NewExcludedPath = @"C:\excluded1" };

        vm.AddExcludedPath();

        Assert.Equal(2, vm.ExcludedPaths.Count);
        Assert.Single(vm.ExcludedPaths, p => p.Equals(@"C:\excluded1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RemoveExcludedPath_RemovesMatchingEntry()
    {
        var vm = new SettingsViewModel(DefaultSettings());

        vm.RemoveExcludedPath(@"C:\excluded1");

        Assert.DoesNotContain(@"C:\excluded1", vm.ExcludedPaths);
        Assert.Single(vm.ExcludedPaths);
    }
}
