using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DOTest_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private SettingsService MakeSut() => new(_tempDir);

    // ── Load ─────────────────────────────────────────────────────

    [Fact]
    public void Load_WhenFileDoesNotExist_UsesDefaultConfig()
    {
        var sut = MakeSut();
        sut.Load();

        Assert.NotNull(sut.Config);
        Assert.Empty(sut.Config.Containers);
    }

    [Fact]
    public void Load_WithCorruptFile_UsesDefaultConfig()
    {
        Directory.CreateDirectory(_tempDir);
        var configPath = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(configPath, "NOT VALID JSON {{{{");

        var sut = MakeSut();
        sut.Load();

        Assert.NotNull(sut.Config);
        Assert.Empty(sut.Config.Containers);
    }

    // ── Save ─────────────────────────────────────────────────────

    [Fact]
    public void Save_CreatesDirectoryIfNotExists()
    {
        var sut = MakeSut();
        sut.Save();

        Assert.True(Directory.Exists(_tempDir));
    }

    [Fact]
    public void Save_WritesJsonFile()
    {
        var sut = MakeSut();
        sut.Save();

        var configPath = Path.Combine(_tempDir, "config.json");
        Assert.True(File.Exists(configPath));
    }

    // ── Round-trip ───────────────────────────────────────────────

    [Fact]
    public void SaveThenLoad_RoundtripsContainerList()
    {
        var sut = MakeSut();
        sut.Config.Containers.Add(new Container { Name = "테스트", X = 10, Y = 20 });
        sut.Save();

        var sut2 = MakeSut();
        sut2.Load();

        Assert.Single(sut2.Config.Containers);
        Assert.Equal("테스트", sut2.Config.Containers[0].Name);
        Assert.Equal(10, sut2.Config.Containers[0].X);
        Assert.Equal(20, sut2.Config.Containers[0].Y);
    }

    [Fact]
    public void SaveThenLoad_PreservesContainerId()
    {
        var id  = Guid.NewGuid();
        var sut = MakeSut();
        sut.Config.Containers.Add(new Container { Id = id, Name = "ID 유지 테스트" });
        sut.Save();

        var sut2 = MakeSut();
        sut2.Load();

        Assert.Equal(id, sut2.Config.Containers[0].Id);
    }

    [Fact]
    public void SaveThenLoad_PreservesMultipleContainers()
    {
        var sut = MakeSut();
        sut.Config.Containers.Add(new Container { Name = "A" });
        sut.Config.Containers.Add(new Container { Name = "B" });
        sut.Config.Containers.Add(new Container { Name = "C" });
        sut.Save();

        var sut2 = MakeSut();
        sut2.Load();

        Assert.Equal(3, sut2.Config.Containers.Count);
    }

    [Fact]
    public void SaveThenLoad_PreservesContainerStyle()
    {
        var sut = MakeSut();
        sut.Config.Containers.Add(new Container
        {
            Name  = "스타일 테스트",
            Style = new ContainerStyle { BackgroundColor = "#FF0000", CornerRadius = 8.0 }
        });
        sut.Save();

        var sut2 = MakeSut();
        sut2.Load();

        Assert.Equal("#FF0000", sut2.Config.Containers[0].Style.BackgroundColor);
        Assert.Equal(8.0, sut2.Config.Containers[0].Style.CornerRadius);
    }

    [Fact]
    public void SaveThenLoad_PreservesVersion()
    {
        var sut = MakeSut();
        sut.Save();

        var sut2 = MakeSut();
        sut2.Load();

        Assert.Equal("1.0.0", sut2.Config.Version);
    }
}
