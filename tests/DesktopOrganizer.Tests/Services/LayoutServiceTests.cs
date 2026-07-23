using System.Windows;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

public class LayoutServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _settings;

    public LayoutServiceTests()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), $"DOLayoutTest_{Guid.NewGuid():N}");
        _settings = new SettingsService(_tempDir);
        _settings.Load();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private TestableLayoutService MakeSut() => new(_settings);

    private sealed class TestableLayoutService : LayoutService
    {
        public List<Dictionary<string, (int X, int Y)>> PositionWrites { get; } = new();

        public TestableLayoutService(SettingsService settings) : base(settings) { }

        protected override void WritePositions(Dictionary<string, (int X, int Y)> positions)
            => PositionWrites.Add(new Dictionary<string, (int X, int Y)>(positions));
    }

    private static Layout MakeLayout(string name = "Test Layout") => new()
    {
        Name         = name,
        SavedAt      = DateTime.UtcNow,
        ScreenWidth  = 1920,
        ScreenHeight = 1080,
    };

    // ── F-020: Save & GetAll ──────────────────────────────────────

    [Fact]
    public void Save_ThenGetAll_ContainsLayout()
    {
        var sut    = MakeSut();
        var layout = MakeLayout("MyLayout");
        sut.Save(layout);

        var all = sut.GetAll();

        Assert.Single(all);
        Assert.Equal("MyLayout", all[0].Name);
    }

    [Fact]
    public void GetAll_SortsNewestFirst()
    {
        var sut    = MakeSut();
        var old    = MakeLayout("Old");
        old.SavedAt = DateTime.UtcNow.AddHours(-1);
        var newest = MakeLayout("Newest");
        newest.SavedAt = DateTime.UtcNow;
        sut.Save(old);
        sut.Save(newest);

        var all = sut.GetAll();

        Assert.Equal("Newest", all[0].Name);
        Assert.Equal("Old",    all[1].Name);
    }

    [Fact]
    public void GetAll_SkipsCorruptFiles()
    {
        var sut = MakeSut();
        sut.Save(MakeLayout());
        Directory.CreateDirectory(_settings.LayoutsDir);
        File.WriteAllText(Path.Combine(_settings.LayoutsDir, $"{Guid.NewGuid()}.json"), "CORRUPT");

        var all = sut.GetAll();

        Assert.Single(all);
    }

    // ── Load ─────────────────────────────────────────────────────

    [Fact]
    public void Load_AfterSave_RoundtripsData()
    {
        var sut    = MakeSut();
        var layout = MakeLayout("Round-trip");
        layout.Containers.Add(new LayoutContainerSnapshot
        {
            ContainerId   = Guid.NewGuid(),
            ContainerName = "Box",
            X = 50, Y = 80, Width = 200, Height = 150
        });
        sut.Save(layout);

        var loaded = sut.Load(layout.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Round-trip", loaded!.Name);
        Assert.Single(loaded.Containers);
        Assert.Equal("Box", loaded.Containers[0].ContainerName);
        Assert.Equal(50.0, loaded.Containers[0].X);
    }

    [Fact]
    public void Load_NonExistentId_ReturnsNull()
    {
        var sut = MakeSut();
        Assert.Null(sut.Load(Guid.NewGuid()));
    }

    // ── Delete ───────────────────────────────────────────────────

    [Fact]
    public void Delete_RemovesLayoutFromGetAll()
    {
        var sut    = MakeSut();
        var layout = MakeLayout();
        sut.Save(layout);

        sut.Delete(layout.Id);

        Assert.Empty(sut.GetAll());
    }

    [Fact]
    public void Delete_NonExistentId_ReturnsFalse()
    {
        var sut = MakeSut();
        Assert.False(sut.Delete(Guid.NewGuid()));
    }

    // ── GetCount ─────────────────────────────────────────────────

    [Fact]
    public void GetCount_ReturnsNumberOfSavedLayouts()
    {
        var sut = MakeSut();
        sut.Save(MakeLayout("A"));
        sut.Save(MakeLayout("B"));
        sut.Save(MakeLayout("C"));

        Assert.Equal(3, sut.GetCount());
    }

    // ── F-021: Restore ────────────────────────────────────────────

    [Fact]
    public void Restore_AddsContainersToSettings()
    {
        var sut    = MakeSut();
        var layout = MakeLayout();
        layout.Containers.Add(new LayoutContainerSnapshot
        {
            ContainerId   = Guid.NewGuid(),
            ContainerName = "Restored",
            X = 10, Y = 20, Width = 300, Height = 200
        });

        sut.Restore(layout, _settings, Array.Empty<IconInfo>());

        Assert.Single(_settings.Config.Containers);
        Assert.Equal("Restored", _settings.Config.Containers[0].Name);
    }

    [Fact]
    public void Restore_ClearsPreviousContainers()
    {
        var sut = MakeSut();
        _settings.Config.Containers.Add(new Container { Name = "Old" });

        var layout = MakeLayout();
        layout.Containers.Add(new LayoutContainerSnapshot
        {
            ContainerId = Guid.NewGuid(), ContainerName = "New",
            X = 0, Y = 0, Width = 200, Height = 100
        });

        sut.Restore(layout, _settings, Array.Empty<IconInfo>());

        Assert.Single(_settings.Config.Containers);
        Assert.Equal("New", _settings.Config.Containers[0].Name);
    }

    [Fact]
    public void Restore_WithMissingIcons_ReturnsMissingPaths()
    {
        var sut    = MakeSut();
        var layout = MakeLayout();
        layout.Containers.Add(new LayoutContainerSnapshot
        {
            ContainerId   = Guid.NewGuid(),
            ContainerName = "Container",
            X = 0, Y = 0, Width = 400, Height = 300,
            Icons = new List<LayoutIconPlacement>
            {
                new() { IconPath = @"C:\fake\missing.lnk", OrderIndex = 0 }
            }
        });

        var missing = sut.Restore(layout, _settings, Array.Empty<IconInfo>());

        Assert.Single(missing);
        Assert.Equal(@"C:\fake\missing.lnk", missing[0]);
    }

    [Fact]
    public void Restore_MatchingIcon_CallsWritePositions()
    {
        var sut      = MakeSut();
        var iconPath = @"C:\Desktop\MyApp.lnk";
        var layout   = MakeLayout();
        layout.Containers.Add(new LayoutContainerSnapshot
        {
            ContainerId   = Guid.NewGuid(),
            ContainerName = "Container",
            X = 100, Y = 50, Width = 400, Height = 300,
            Icons = new List<LayoutIconPlacement>
            {
                new() { IconPath = iconPath, OrderIndex = 0 }
            }
        });
        var currentIcons = new[]
        {
            new IconInfo { FullPath = iconPath, FileName = "MyApp.lnk", Extension = ".lnk" }
        };

        sut.Restore(layout, _settings, currentIcons);

        Assert.Single(sut.PositionWrites);
        Assert.True(sut.PositionWrites[0].ContainsKey("MyApp")); // .lnk → strip extension
    }

    [Fact]
    public void Restore_WithResolutionMismatch_ScalesContainerPosition()
    {
        var sut = MakeSut();
        const int savedWidth  = 960;
        const int savedHeight = 540;

        var layout = new Layout
        {
            Name         = "Scale Test",
            SavedAt      = DateTime.UtcNow,
            ScreenWidth  = savedWidth,
            ScreenHeight = savedHeight
        };
        layout.Containers.Add(new LayoutContainerSnapshot
        {
            ContainerId   = Guid.NewGuid(),
            ContainerName = "Scaled",
            X = 100, Y = 100, Width = 200, Height = 150
        });

        sut.Restore(layout, _settings, Array.Empty<IconInfo>());

        var c        = _settings.Config.Containers[0];
        var currentW = (int)SystemParameters.PrimaryScreenWidth;
        var currentH = (int)SystemParameters.PrimaryScreenHeight;
        var expectedX = 100.0 * currentW / savedWidth;
        var expectedY = 100.0 * currentH / savedHeight;
        Assert.Equal(expectedX, c.X, precision: 1);
        Assert.Equal(expectedY, c.Y, precision: 1);
    }
}
