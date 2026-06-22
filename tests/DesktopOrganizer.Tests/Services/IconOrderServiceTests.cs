using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

public class IconOrderServiceTests : IDisposable
{
    private readonly string           _tempDir;
    private readonly SettingsService  _settings;
    private readonly IconOrderService _sut;
    private readonly Container        _container;

    public IconOrderServiceTests()
    {
        _tempDir   = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _settings  = new SettingsService(_tempDir);
        _settings.Load();
        _sut       = new IconOrderService(_settings);

        _container = new Container { Id = Guid.NewGuid(), Name = "Test" };
        _settings.Config.Containers.Add(_container);
        _settings.Save();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static IconInfo MakeIcon(string path, int orderIndex = -1) => new()
    {
        FileName   = Path.GetFileName(path),
        FullPath   = path,
        OrderIndex = orderIndex
    };

    // ── SaveIconOrder ─────────────────────────────────────────────

    [Fact]
    public void SaveIconOrder_StoresPathAndIndex()
    {
        var icons = new[]
        {
            MakeIcon(@"C:\Desktop\A.txt"),
            MakeIcon(@"C:\Desktop\B.txt")
        };

        _sut.SaveIconOrder(_container.Id, icons);

        Assert.Equal(2, _container.IconOrder.Count);
        Assert.Equal(@"C:\Desktop\A.txt", _container.IconOrder[0].IconPath);
        Assert.Equal(0, _container.IconOrder[0].OrderIndex);
        Assert.Equal(@"C:\Desktop\B.txt", _container.IconOrder[1].IconPath);
        Assert.Equal(1, _container.IconOrder[1].OrderIndex);
    }

    [Fact]
    public void SaveIconOrder_OverwritesPreviousOrder()
    {
        var first  = new[] { MakeIcon(@"C:\Desktop\Old.txt") };
        var second = new[] { MakeIcon(@"C:\Desktop\New.txt") };

        _sut.SaveIconOrder(_container.Id, first);
        _sut.SaveIconOrder(_container.Id, second);

        Assert.Single(_container.IconOrder);
        Assert.Equal(@"C:\Desktop\New.txt", _container.IconOrder[0].IconPath);
    }

    [Fact]
    public void SaveIconOrder_SavesToDisk()
    {
        var icons = new[] { MakeIcon(@"C:\Desktop\A.txt") };
        _sut.SaveIconOrder(_container.Id, icons);

        var s2 = new SettingsService(_tempDir);
        s2.Load();
        var c2 = s2.Config.Containers.First(c => c.Id == _container.Id);
        Assert.Single(c2.IconOrder);
        Assert.Equal(@"C:\Desktop\A.txt", c2.IconOrder[0].IconPath);
    }

    [Fact]
    public void SaveIconOrder_WithUnknownContainerId_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            _sut.SaveIconOrder(Guid.NewGuid(), new[] { MakeIcon(@"C:\x.txt") }));
        Assert.Null(ex);
    }

    // ── RestoreIconOrder ──────────────────────────────────────────

    [Fact]
    public void RestoreIconOrder_ReordersToSavedSequence()
    {
        // Save in order: B then A
        var saveOrder = new[]
        {
            MakeIcon(@"C:\Desktop\B.txt"),
            MakeIcon(@"C:\Desktop\A.txt")
        };
        _sut.SaveIconOrder(_container.Id, saveOrder);

        // Restore with icons in different order
        var current = new List<IconInfo>
        {
            MakeIcon(@"C:\Desktop\A.txt"),
            MakeIcon(@"C:\Desktop\B.txt")
        };
        var result = _sut.RestoreIconOrder(_container.Id, current);

        Assert.Equal(@"C:\Desktop\B.txt", result[0].FullPath);
        Assert.Equal(@"C:\Desktop\A.txt", result[1].FullPath);
    }

    [Fact]
    public void RestoreIconOrder_SetsOrderIndex()
    {
        var saved = new[] { MakeIcon(@"C:\A.txt"), MakeIcon(@"C:\B.txt") };
        _sut.SaveIconOrder(_container.Id, saved);

        var current = new List<IconInfo>
        {
            MakeIcon(@"C:\B.txt"),
            MakeIcon(@"C:\A.txt")
        };
        var result = _sut.RestoreIconOrder(_container.Id, current);

        Assert.Equal(0, result[0].OrderIndex);
        Assert.Equal(1, result[1].OrderIndex);
    }

    [Fact]
    public void RestoreIconOrder_UnknownIconsAppendedAtEnd()
    {
        // Save order for A only
        _sut.SaveIconOrder(_container.Id, new[] { MakeIcon(@"C:\A.txt") });

        var current = new List<IconInfo>
        {
            MakeIcon(@"C:\B.txt"),  // not in saved order
            MakeIcon(@"C:\A.txt")
        };
        var result = _sut.RestoreIconOrder(_container.Id, current);

        Assert.Equal(@"C:\A.txt", result[0].FullPath);
        Assert.Equal(@"C:\B.txt", result[1].FullPath);
    }

    [Fact]
    public void RestoreIconOrder_StaleEntriesInSavedOrder_AreIgnored()
    {
        // Save with a path that no longer exists
        _sut.SaveIconOrder(_container.Id, new[]
        {
            MakeIcon(@"C:\Deleted.txt"),
            MakeIcon(@"C:\Existing.txt")
        });

        var current = new List<IconInfo> { MakeIcon(@"C:\Existing.txt") };
        var result  = _sut.RestoreIconOrder(_container.Id, current);

        // Should not throw; the stale entry is just skipped
        Assert.Single(result);
        Assert.Equal(@"C:\Existing.txt", result[0].FullPath);
    }

    [Fact]
    public void RestoreIconOrder_NoSavedOrder_ReturnsOriginalListWithIndices()
    {
        var current = new List<IconInfo>
        {
            MakeIcon(@"C:\A.txt"),
            MakeIcon(@"C:\B.txt")
        };
        var result = _sut.RestoreIconOrder(_container.Id, current);

        Assert.Equal(@"C:\A.txt", result[0].FullPath);
        Assert.Equal(0, result[0].OrderIndex);
        Assert.Equal(@"C:\B.txt", result[1].FullPath);
        Assert.Equal(1, result[1].OrderIndex);
    }

    // ── CleanupStaleEntries ───────────────────────────────────────

    [Fact]
    public void CleanupStaleEntries_RemovesPathsNotInExistingSet()
    {
        _sut.SaveIconOrder(_container.Id, new[]
        {
            MakeIcon(@"C:\Alive.txt"),
            MakeIcon(@"C:\Dead.txt")
        });

        _sut.CleanupStaleEntries(_container.Id, new[] { @"C:\Alive.txt" });

        Assert.Single(_container.IconOrder);
        Assert.Equal(@"C:\Alive.txt", _container.IconOrder[0].IconPath);
    }

    [Fact]
    public void CleanupStaleEntries_SavesToDiskAfterRemoval()
    {
        _sut.SaveIconOrder(_container.Id, new[]
        {
            MakeIcon(@"C:\Alive.txt"),
            MakeIcon(@"C:\Dead.txt")
        });

        _sut.CleanupStaleEntries(_container.Id, new[] { @"C:\Alive.txt" });

        var s2 = new SettingsService(_tempDir);
        s2.Load();
        var c2 = s2.Config.Containers.First(c => c.Id == _container.Id);
        Assert.Single(c2.IconOrder);
    }

    [Fact]
    public void CleanupStaleEntries_NothingToRemove_DoesNotThrow()
    {
        _sut.SaveIconOrder(_container.Id, new[] { MakeIcon(@"C:\A.txt") });
        var ex = Record.Exception(() =>
            _sut.CleanupStaleEntries(_container.Id, new[] { @"C:\A.txt" }));
        Assert.Null(ex);
    }

    [Fact]
    public void CleanupStaleEntries_PathComparison_IsCaseInsensitive()
    {
        _sut.SaveIconOrder(_container.Id, new[] { MakeIcon(@"C:\File.txt") });

        // Pass uppercase path — should still match and NOT remove the entry
        _sut.CleanupStaleEntries(_container.Id, new[] { @"C:\FILE.TXT" });

        Assert.Single(_container.IconOrder);
    }
}
