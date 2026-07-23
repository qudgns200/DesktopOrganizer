using System.IO;
using System.Text;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

public enum LogLevel { Debug = 0, Info = 1, Warn = 2, Error = 3, Off = 4 }

/// <summary>
/// F-022: Thread-safe daily file logger.
/// - Rolls over to a new archive file when the current file exceeds 10 MB.
/// - Auto-deletes log files older than 30 days on startup.
/// - Never throws — logging failures are silently swallowed so the app keeps running.
/// Access the process-wide instance via <see cref="Instance"/>.
/// </summary>
public sealed class LogService : IDisposable
{
    private const long MaxFileSizeBytes = 10L * 1024 * 1024; // 10 MB
    private const int  MaxRetentionDays = 30;

    private static readonly string DefaultLogsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DesktopOrganizer", "logs");

    private static readonly Lazy<LogService> _lazy =
        new(() => new LogService(DefaultLogsDir));

    /// <summary>Application-wide singleton — initialised on first access.</summary>
    public static LogService Instance => _lazy.Value;

    private readonly string _logsDir;
    private readonly object _lock = new();
    private string _currentDate     = string.Empty;
    private string _currentFilePath = string.Empty;
    private int    _archiveIndex    = 0;

    /// <summary>Minimum level written to disk; messages below this are silently discarded.</summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Info;

    /// <summary>Creates an instance that writes to <paramref name="logsDir"/>.</summary>
    public LogService(string logsDir)
    {
        _logsDir = logsDir;
        CleanupOldLogs();
    }

    // ── Public API ────────────────────────────────────────────────

    public void Info (string featureId, string message) => Write(LogLevel.Info,  featureId, message);
    public void Warn (string featureId, string message) => Write(LogLevel.Warn,  featureId, message);
    public void Error(string featureId, string message) => Write(LogLevel.Error, featureId, message);
    public void Debug(string featureId, string message) => Write(LogLevel.Debug, featureId, message);

    // ── Core write ────────────────────────────────────────────────

    private void Write(LogLevel level, string featureId, string message)
    {
        if (level < MinLevel) return;
        var utc  = DateTime.UtcNow;
        var line = FormatLine(utc, level, featureId, message);
        Append(utc, line);
    }

    private static string FormatLine(DateTime utc, LogLevel level, string featureId, string message)
        => $"{utc:yyyy-MM-dd HH:mm:ss.fff} UTC  [{level.ToString().ToUpperInvariant(),-5}]  {featureId,-6}  {message}";

    private void Append(DateTime utc, string line)
    {
        lock (_lock)
        {
            try
            {
                var dateStr = utc.ToString("yyyyMMdd");

                // Switch to a new base file when the date rolls over
                if (dateStr != _currentDate)
                {
                    _currentDate     = dateStr;
                    _archiveIndex    = 0;
                    _currentFilePath = BuildFilePath(dateStr, 0);
                    Directory.CreateDirectory(_logsDir);
                }

                // Roll to next archive file if current file exceeds 10 MB
                if (File.Exists(_currentFilePath) &&
                    new FileInfo(_currentFilePath).Length >= MaxFileSizeBytes)
                {
                    _archiveIndex++;
                    _currentFilePath = BuildFilePath(_currentDate, _archiveIndex);
                }

                File.AppendAllText(_currentFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // F-022 spec: logging must never crash the app
            }
        }
    }

    // ── 30-day cleanup ────────────────────────────────────────────

    private void CleanupOldLogs()
    {
        try
        {
            if (!Directory.Exists(_logsDir)) return;
            var cutoff = DateTime.UtcNow.AddDays(-MaxRetentionDays);
            foreach (var file in Directory.EnumerateFiles(_logsDir, "*.log"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                        File.Delete(file);
                }
                catch { /* skip individual file errors */ }
            }
        }
        catch { /* directory error — ignore */ }
    }

    // ── Helper ───────────────────────────────────────────────────

    private string BuildFilePath(string dateStr, int index)
    {
        var suffix = index == 0 ? string.Empty : $"_{index}";
        return Path.Combine(_logsDir, $"desktop_organizer_{dateStr}{suffix}.log");
    }

    public void Dispose() { /* streams opened per-write — nothing to release */ }
}

/// <summary>Converts <see cref="AppLogLevel"/> from settings to the internal <see cref="LogLevel"/>.</summary>
public static class AppLogLevelExtensions
{
    public static LogLevel ToLogLevel(this AppLogLevel level) => level switch
    {
        AppLogLevel.Disabled  => LogLevel.Off,
        AppLogLevel.ErrorOnly => LogLevel.Warn,  // WARN+ERROR (F-022 spec)
        AppLogLevel.Info      => LogLevel.Info,
        AppLogLevel.Debug     => LogLevel.Debug,
        _                     => LogLevel.Info
    };
}
