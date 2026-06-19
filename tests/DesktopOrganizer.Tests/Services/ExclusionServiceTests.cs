using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

public class ExclusionServiceTests
{
    private static ExclusionService CreateService(AppSettings? settings = null)
        => new(settings ?? new AppSettings());

    private static IconInfo MakeIcon(string fileName, string fullPath)
        => new() { FileName = fileName, FullPath = fullPath };

    // ── Layer 2: virtual items don't exist on disk ───────────
    [Fact]
    public void IsExcluded_NonExistentPath_ReturnsTrue()
    {
        var svc = CreateService();
        var icon = MakeIcon("This PC", @"C:\__NONEXISTENT_VIRTUAL_ICON__");
        Assert.True(svc.IsExcluded(icon));
    }

    [Fact]
    public void IsExcluded_RealFile_ReturnsFalse()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var svc = CreateService();
            var icon = MakeIcon(Path.GetFileName(tmp), tmp);
            Assert.False(svc.IsExcluded(icon));
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void IsExcluded_RealDirectory_ReturnsFalse()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        try
        {
            var svc = CreateService();
            var icon = MakeIcon(Path.GetFileName(tmp), tmp);
            Assert.False(svc.IsExcluded(icon));
        }
        finally { Directory.Delete(tmp); }
    }

    // ── Layer 1: user-added path exclusions ─────────────────
    [Fact]
    public void IsExcluded_UserExcludedPath_ReturnsTrue_EvenIfFileExists()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var settings = new AppSettings();
            settings.ExcludedPaths.Add(tmp);
            var svc = new ExclusionService(settings);
            var icon = MakeIcon(Path.GetFileName(tmp), tmp);
            Assert.True(svc.IsExcluded(icon));
        }
        finally { File.Delete(tmp); }
    }

    // ── Layer 3: display-name fallback ──────────────────────
    [Theory]
    [InlineData("Recycle Bin")]
    [InlineData("This PC")]
    [InlineData("휴지통")]
    [InlineData("내 PC")]
    [InlineData("Network")]
    public void IsExcluded_SystemDisplayName_ReturnsTrue(string displayName)
    {
        var svc = CreateService();
        // Path does not exist → caught by Layer 2 already, but this also tests Layer 3
        var icon = MakeIcon(displayName, @"C:\__FAKE_VIRTUAL__");
        Assert.True(svc.IsExcluded(icon));
    }

    // ── ApplyExclusion ───────────────────────────────────────
    [Fact]
    public void ApplyExclusion_SetsIsSystemIconFlag_Correctly()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var svc = CreateService();
            var icons = new List<IconInfo>
            {
                MakeIcon("This PC",                   @"C:\__VIRTUAL__"),  // excluded
                MakeIcon(Path.GetFileName(tmp), tmp),                       // real file → not excluded
            };

            svc.ApplyExclusion(icons);

            Assert.True(icons[0].IsSystemIcon);
            Assert.False(icons[1].IsSystemIcon);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void ApplyExclusion_EmptyList_DoesNotThrow()
    {
        var svc = CreateService();
        var ex = Record.Exception(() => svc.ApplyExclusion(new List<IconInfo>()));
        Assert.Null(ex);
    }
}
