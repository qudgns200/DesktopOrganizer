using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

public class LogServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LogService _sut;

    public LogServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DOLogTest_{Guid.NewGuid():N}");
        _sut     = new LogService(_tempDir);
    }

    public void Dispose()
    {
        _sut.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Write helpers ─────────────────────────────────────────────

    private string[] LogFiles()
        => Directory.Exists(_tempDir)
            ? Directory.GetFiles(_tempDir, "*.log")
            : Array.Empty<string>();

    private string AllContent()
        => string.Concat(LogFiles().Select(File.ReadAllText));

    // ── Level writing ─────────────────────────────────────────────

    [Fact]
    public void Info_WritesInfoLine()
    {
        _sut.MinLevel = LogLevel.Info;
        _sut.Info("F-001", "info message");

        Assert.Contains("INFO",         AllContent());
        Assert.Contains("F-001",        AllContent());
        Assert.Contains("info message", AllContent());
    }

    [Fact]
    public void Warn_WritesWarnLine()
    {
        _sut.MinLevel = LogLevel.Debug;
        _sut.Warn("F-002", "warn message");

        Assert.Contains("WARN",         AllContent());
        Assert.Contains("warn message", AllContent());
    }

    [Fact]
    public void Error_WritesErrorLine()
    {
        _sut.MinLevel = LogLevel.Debug;
        _sut.Error("F-003", "error message");

        Assert.Contains("ERROR",         AllContent());
        Assert.Contains("error message", AllContent());
    }

    [Fact]
    public void Debug_WritesDebugLine()
    {
        _sut.MinLevel = LogLevel.Debug;
        _sut.Debug("F-004", "debug message");

        Assert.Contains("DEBUG",         AllContent());
        Assert.Contains("debug message", AllContent());
    }

    // ── MinLevel filtering ────────────────────────────────────────

    [Fact]
    public void Log_BelowMinLevel_NotWritten()
    {
        _sut.MinLevel = LogLevel.Error;
        _sut.Info("F-001", "should be filtered");

        Assert.DoesNotContain("should be filtered", AllContent());
    }

    [Fact]
    public void Log_ExactlyAtMinLevel_Written()
    {
        _sut.MinLevel = LogLevel.Warn;
        _sut.Warn("F-001", "exactly at min level");

        Assert.Contains("exactly at min level", AllContent());
    }

    [Fact]
    public void Log_AboveMinLevel_Written()
    {
        _sut.MinLevel = LogLevel.Warn;
        _sut.Error("F-001", "above min level");

        Assert.Contains("above min level", AllContent());
    }

    [Fact]
    public void MinLevel_Off_NothingWritten()
    {
        _sut.MinLevel = LogLevel.Off;
        _sut.Error("F-001", "nothing should write");

        Assert.Empty(LogFiles());
    }

    // ── Format ────────────────────────────────────────────────────

    [Fact]
    public void Log_EntryContainsUtcTimestamp()
    {
        _sut.MinLevel = LogLevel.Info;
        _sut.Info("F-001", "timestamp check");

        Assert.Contains("UTC", AllContent());
    }

    [Fact]
    public void Log_EntryContainsFeatureId()
    {
        _sut.MinLevel = LogLevel.Info;
        _sut.Info("F-022", "feature id check");

        Assert.Contains("F-022", AllContent());
    }

    // ── File management ───────────────────────────────────────────

    [Fact]
    public void Log_FileNameContainsTodayDate()
    {
        _sut.MinLevel = LogLevel.Info;
        _sut.Info("F-001", "date test");

        var expectedDate = DateTime.UtcNow.ToString("yyyyMMdd");
        Assert.Contains(LogFiles(), f => Path.GetFileName(f).Contains(expectedDate));
    }

    [Fact]
    public void Log_MultipleEntries_WrittenToSameFile()
    {
        _sut.MinLevel = LogLevel.Info;
        _sut.Info("F-001", "first");
        _sut.Info("F-001", "second");
        _sut.Info("F-001", "third");

        Assert.Single(LogFiles());
        var content = AllContent();
        Assert.Contains("first",  content);
        Assert.Contains("second", content);
        Assert.Contains("third",  content);
    }

    // ── 30-day cleanup ────────────────────────────────────────────

    [Fact]
    public void Cleanup_DeletesFilesOlderThan30Days()
    {
        Directory.CreateDirectory(_tempDir);
        var oldFile = Path.Combine(_tempDir, "desktop_organizer_20250101.log");
        File.WriteAllText(oldFile, "old content");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-31));

        // New instance triggers cleanup in constructor
        using var sut2 = new LogService(_tempDir);

        Assert.False(File.Exists(oldFile));
    }

    [Fact]
    public void Cleanup_KeepsFilesNewerThan30Days()
    {
        Directory.CreateDirectory(_tempDir);
        var recentFile = Path.Combine(_tempDir, "desktop_organizer_20260720.log");
        File.WriteAllText(recentFile, "recent content");
        // last-write defaults to now — well within 30 days

        using var sut2 = new LogService(_tempDir);

        Assert.True(File.Exists(recentFile));
    }

    // ── Resilience ────────────────────────────────────────────────

    [Fact]
    public void Log_InvalidDirectory_DoesNotThrow()
    {
        // Use an effectively unwritable path (long path that can't be created)
        using var sut = new LogService(@"Z:\nonexistent\path\that\cannot\exist");
        var ex = Record.Exception(() => sut.Error("F-001", "must not throw"));
        Assert.Null(ex);
    }

    // ── AppLogLevel extension ─────────────────────────────────────

    [Theory]
    [InlineData(AppLogLevel.Disabled,  LogLevel.Off)]
    [InlineData(AppLogLevel.ErrorOnly, LogLevel.Warn)]
    [InlineData(AppLogLevel.Info,      LogLevel.Info)]
    [InlineData(AppLogLevel.Debug,     LogLevel.Debug)]
    public void AppLogLevelExtensions_MapsCorrectly(AppLogLevel appLevel, LogLevel expected)
    {
        Assert.Equal(expected, appLevel.ToLogLevel());
    }
}
