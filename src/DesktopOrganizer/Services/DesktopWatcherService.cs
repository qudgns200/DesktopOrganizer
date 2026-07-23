using System.Collections.Concurrent;
using System.IO;
using DesktopOrganizer.Interop;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-016: Monitors the desktop folder(s) for file system changes and raises
/// <see cref="DesktopChanged"/> events after a 500 ms debounce window.
/// Falls back to 5-second polling when FileSystemWatcher cannot be initialised.
/// </summary>
public sealed class DesktopWatcherService : IDisposable
{
    private const int DebounceMs    = 500;
    private const int PollIntervalMs = 5_000;

    private readonly List<string>  _desktopPaths;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentQueue<DesktopChangeEventArgs> _queue = new();

    private System.Threading.Timer? _debounceTimer;
    private System.Threading.Timer? _pollTimer;
    private readonly object         _timerLock = new();

    // Polling fallback: last-known file set for diff
    private HashSet<string>? _pollSnapshot;

    private bool _running;
    private bool _disposed;

    public event EventHandler<DesktopChangeEventArgs>? DesktopChanged;

    public DesktopWatcherService()
    {
        _desktopPaths = BuildDesktopPaths();
    }

    // ── Public API ────────────────────────────────────────────────

    public void Start()
    {
        if (_running || _disposed) return;
        _running = true;

        bool anyWatcherStarted = false;
        foreach (var path in _desktopPaths)
        {
            if (TryStartWatcher(path))
                anyWatcherStarted = true;
        }

        if (!anyWatcherStarted)
        {
            LogService.Instance.Warn("F-016", "FileSystemWatcher failed for all paths — starting polling fallback");
            StartPolling();
        }
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;

        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();

        lock (_timerLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        _pollTimer?.Dispose();
        _pollTimer    = null;
        _pollSnapshot = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    // ── FileSystemWatcher setup ───────────────────────────────────

    private bool TryStartWatcher(string path)
    {
        try
        {
            var w = new FileSystemWatcher(path)
            {
                NotifyFilter           = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories  = false,
                EnableRaisingEvents    = false
            };

            w.Created += (_, e) => Enqueue(new DesktopChangeEventArgs
            {
                ChangeType = DesktopChangeType.Created,
                FullPath   = e.FullPath
            });

            w.Deleted += (_, e) => Enqueue(new DesktopChangeEventArgs
            {
                ChangeType = DesktopChangeType.Deleted,
                FullPath   = e.FullPath
            });

            w.Renamed += (_, e) => Enqueue(new DesktopChangeEventArgs
            {
                ChangeType  = DesktopChangeType.Renamed,
                FullPath    = e.FullPath,
                OldFullPath = e.OldFullPath
            });

            w.Error += OnWatcherError;

            w.EnableRaisingEvents = true;
            _watchers.Add(w);
            LogService.Instance.Info("F-016", $"Watching: {path}");
            return true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("F-016", $"Failed to watch '{path}': {ex.Message}");
            return false;
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        LogService.Instance.Warn("F-016", $"Watcher error: {e.GetException().Message} — switching to polling");

        // Disable the broken watcher
        if (sender is FileSystemWatcher w)
        {
            w.EnableRaisingEvents = false;
            _watchers.Remove(w);
            w.Dispose();
        }

        // If no watchers remain, fall back to polling
        if (_watchers.Count == 0)
            StartPolling();
    }

    // ── Debounce queue ────────────────────────────────────────────

    private void Enqueue(DesktopChangeEventArgs args)
    {
        _queue.Enqueue(args);

        lock (_timerLock)
        {
            if (_debounceTimer is null)
                _debounceTimer = new System.Threading.Timer(FlushQueue, null, DebounceMs, Timeout.Infinite);
            else
                _debounceTimer.Change(DebounceMs, Timeout.Infinite);
        }
    }

    private void FlushQueue(object? _)
    {
        // Deduplicate same path+type pairs within one batch
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (_queue.TryDequeue(out var e))
        {
            var key = $"{e.ChangeType}:{e.FullPath}";
            if (!seen.Add(key)) continue;
            RaiseEvent(e);
        }
    }

    private void RaiseEvent(DesktopChangeEventArgs e)
    {
        try { DesktopChanged?.Invoke(this, e); }
        catch (Exception ex)
        {
            LogService.Instance.Error("F-016", $"DesktopChanged handler threw: {ex.Message}");
        }
    }

    // ── Polling fallback (F-016: "폴링(5초)으로 대체") ─────────────

    private void StartPolling()
    {
        if (_pollTimer is not null) return;  // already polling
        _pollSnapshot = TakeSnapshot();
        _pollTimer = new System.Threading.Timer(OnPollTick, null, PollIntervalMs, PollIntervalMs);
        LogService.Instance.Info("F-016", "Polling started (5s interval)");
    }

    private void OnPollTick(object? _)
    {
        var current  = TakeSnapshot();
        var previous = _pollSnapshot ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _pollSnapshot = current;

        foreach (var path in current.Except(previous, StringComparer.OrdinalIgnoreCase))
            RaiseEvent(new DesktopChangeEventArgs { ChangeType = DesktopChangeType.Created, FullPath = path });

        foreach (var path in previous.Except(current, StringComparer.OrdinalIgnoreCase))
            RaiseEvent(new DesktopChangeEventArgs { ChangeType = DesktopChangeType.Deleted, FullPath = path });
    }

    private HashSet<string> TakeSnapshot()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in _desktopPaths)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(root))
                    result.Add(entry);
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("F-016", $"Snapshot error for '{root}': {ex.Message}");
            }
        }
        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static List<string> BuildDesktopPaths()
    {
        var paths = new List<string>();
        var user  = ShellApi.GetUserDesktopPath();
        if (!string.IsNullOrEmpty(user) && Directory.Exists(user)) paths.Add(user);

        var pub = ShellApi.GetPublicDesktopPath();
        if (!string.IsNullOrEmpty(pub) && Directory.Exists(pub) &&
            !paths.Contains(pub, StringComparer.OrdinalIgnoreCase))
            paths.Add(pub);

        return paths;
    }
}
