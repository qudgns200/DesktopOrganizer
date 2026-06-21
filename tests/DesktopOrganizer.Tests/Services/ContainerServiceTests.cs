using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

public class ContainerServiceTests : IDisposable
{
    // Each test gets its own temp directory so tests do not interfere
    private readonly string _tempDir;
    private readonly SettingsService _settings;
    private readonly ContainerService _sut;

    public ContainerServiceTests()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), $"DOTest_{Guid.NewGuid():N}");
        _settings = new SettingsService(_tempDir);
        _sut      = new ContainerService(_settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── F-004 Create ─────────────────────────────────────────────

    [Fact]
    public void Create_ReturnsContainerWithExpectedName()
    {
        var c = _sut.Create(100, 200);

        Assert.Equal("새 Container 1", c.Name);
    }

    [Fact]
    public void Create_StoresCoordinates()
    {
        var c = _sut.Create(50.5, 75.25);

        Assert.Equal(50.5, c.X);
        Assert.Equal(75.25, c.Y);
    }

    [Fact]
    public void Create_AssignsUniqueIds()
    {
        var c1 = _sut.Create(0, 0);
        var c2 = _sut.Create(0, 0);

        Assert.NotEqual(c1.Id, c2.Id);
    }

    [Fact]
    public void Create_IncrementsSuffixNumber()
    {
        _sut.Create(0, 0);
        var c2 = _sut.Create(0, 0);

        Assert.Equal("새 Container 2", c2.Name);
    }

    [Fact]
    public void Create_SavesToDisk()
    {
        _sut.Create(0, 0);

        var configPath = Path.Combine(_tempDir, "config.json");
        Assert.True(File.Exists(configPath));
    }

    [Fact]
    public void GetAll_ReflectsCreatedContainers()
    {
        _sut.Create(0, 0);
        _sut.Create(0, 0);

        Assert.Equal(2, _sut.GetAll().Count);
    }

    // ── F-005 Rename ─────────────────────────────────────────────

    [Fact]
    public void Rename_WithValidName_ReturnsTrue()
    {
        var c = _sut.Create(0, 0);

        var result = _sut.Rename(c.Id, "업무");

        Assert.True(result);
        Assert.Equal("업무", c.Name);
    }

    [Fact]
    public void Rename_TrimsWhitespace()
    {
        var c = _sut.Create(0, 0);
        _sut.Rename(c.Id, "  업무  ");

        Assert.Equal("업무", c.Name);
    }

    [Fact]
    public void Rename_WithBlankName_ReturnsFalse()
    {
        var c = _sut.Create(0, 0);
        var originalName = c.Name;

        var result = _sut.Rename(c.Id, "   ");

        Assert.False(result);
        Assert.Equal(originalName, c.Name);
    }

    [Fact]
    public void Rename_WithEmptyName_ReturnsFalse()
    {
        var c = _sut.Create(0, 0);

        Assert.False(_sut.Rename(c.Id, ""));
    }

    [Fact]
    public void Rename_WithUnknownId_ReturnsFalse()
    {
        Assert.False(_sut.Rename(Guid.NewGuid(), "이름"));
    }

    // ── F-006 Delete ─────────────────────────────────────────────

    [Fact]
    public void Delete_WithValidId_ReturnsTrue()
    {
        var c = _sut.Create(0, 0);

        Assert.True(_sut.Delete(c.Id));
    }

    [Fact]
    public void Delete_RemovesContainerFromList()
    {
        var c = _sut.Create(0, 0);
        _sut.Delete(c.Id);

        Assert.Empty(_sut.GetAll());
    }

    [Fact]
    public void Delete_WithUnknownId_ReturnsFalse()
    {
        Assert.False(_sut.Delete(Guid.NewGuid()));
    }

    [Fact]
    public void Delete_SavesToDisk()
    {
        var c = _sut.Create(0, 0);
        _sut.Delete(c.Id);

        // Reload from disk and confirm deletion was persisted
        var settings2 = new SettingsService(_tempDir);
        settings2.Load();
        Assert.Empty(settings2.Config.Containers);
    }
}
