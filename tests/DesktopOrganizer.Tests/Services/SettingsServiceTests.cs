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

    // ── F-018: Backup rotation ───────────────────────────────────

    [Fact]
    public void Save_SecondSave_CreatesBak1()
    {
        var sut = MakeSut();
        sut.Save(); // creates config.json
        sut.Save(); // rotates: bak1 ← config.json

        Assert.True(File.Exists(Path.Combine(_tempDir, "config.json.bak1")));
    }

    [Fact]
    public void Save_ThirdSave_CreatesBak2()
    {
        var sut = MakeSut();
        sut.Save();
        sut.Save();
        sut.Save(); // bak2 ← bak1

        Assert.True(File.Exists(Path.Combine(_tempDir, "config.json.bak2")));
    }

    [Fact]
    public void Save_FourSaves_NoBak4Created()
    {
        var sut = MakeSut();
        sut.Save();
        sut.Save();
        sut.Save();
        sut.Save(); // bak3 dropped, no bak4

        Assert.False(File.Exists(Path.Combine(_tempDir, "config.json.bak4")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "config.json.bak3")));
    }

    // ── F-019: Backup restore ────────────────────────────────────

    [Fact]
    public void Load_CorruptMainFile_LoadsFromBak1()
    {
        var sut = MakeSut();
        sut.Config.Containers.Add(new Container { Name = "Bak1Data" });
        sut.Save(); // config.json with Bak1Data
        sut.Save(); // bak1 ← Bak1Data config

        File.WriteAllText(Path.Combine(_tempDir, "config.json"), "CORRUPT");

        var sut2 = MakeSut();
        sut2.Load();

        Assert.Single(sut2.Config.Containers);
        Assert.Equal("Bak1Data", sut2.Config.Containers[0].Name);
    }

    [Fact]
    public void Load_CorruptMainAndBak1_LoadsFromBak2()
    {
        var sut = MakeSut();
        sut.Config.Containers.Add(new Container { Name = "Bak2Data" });
        sut.Save(); // config.json with Bak2Data
        sut.Save(); // bak1 ← Bak2Data
        sut.Save(); // bak2 ← Bak2Data

        File.WriteAllText(Path.Combine(_tempDir, "config.json"),        "CORRUPT");
        File.WriteAllText(Path.Combine(_tempDir, "config.json.bak1"),   "CORRUPT");

        var sut2 = MakeSut();
        sut2.Load();

        Assert.Single(sut2.Config.Containers);
        Assert.Equal("Bak2Data", sut2.Config.Containers[0].Name);
    }

    [Fact]
    public void Load_AllBackupsCorrupt_ReturnsDefaultConfig()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "config.json"),        "CORRUPT");
        File.WriteAllText(Path.Combine(_tempDir, "config.json.bak1"),   "CORRUPT");
        File.WriteAllText(Path.Combine(_tempDir, "config.json.bak2"),   "CORRUPT");
        File.WriteAllText(Path.Combine(_tempDir, "config.json.bak3"),   "CORRUPT");

        var sut = MakeSut();
        sut.Load();

        Assert.NotNull(sut.Config);
        Assert.Empty(sut.Config.Containers);
    }
}
