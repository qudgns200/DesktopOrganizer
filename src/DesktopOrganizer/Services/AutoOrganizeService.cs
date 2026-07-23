using System.Diagnostics;
using System.IO;
using DesktopOrganizer.Interop;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-017: Reacts to desktop file-system events (from <see cref="DesktopWatcherService"/>)
/// and automatically places new icons into the first matching Container based on active Rules.
/// Maintains an in-memory registry of all known desktop icons and their Container assignments.
/// Does NOT move, copy, or delete any actual files — only adjusts desktop icon coordinates.
/// </summary>
public class AutoOrganizeService : IDisposable
{
    private readonly DesktopReaderService   _reader;
    private readonly ExclusionService       _exclusion;
    private readonly FileClassifierService  _classifier;
    private readonly RuleService            _rules;
    private readonly SettingsService        _settings;
    private readonly DesktopWatcherService  _watcher;
    private readonly IconSortService        _sortService;
    private readonly IconOrderService       _orderService;

    // fullPath → IconInfo (all known desktop icons)
    private readonly Dictionary<string, IconInfo> _icons =
        new(StringComparer.OrdinalIgnoreCase);

    // containerId → ordered icon list for that container
    private readonly Dictionary<Guid, List<IconInfo>> _containerIcons = new();

    private readonly object _lock = new();
    private bool _disposed;

    public AutoOrganizeService(
        DesktopReaderService  reader,
        ExclusionService      exclusion,
        FileClassifierService classifier,
        RuleService           rules,
        SettingsService       settings,
        DesktopWatcherService watcher,
        IconSortService       sortService,
        IconOrderService      orderService)
    {
        _reader       = reader;
        _exclusion    = exclusion;
        _classifier   = classifier;
        _rules        = rules;
        _settings     = settings;
        _watcher      = watcher;
        _sortService  = sortService;
        _orderService = orderService;
    }

    // ── Startup ───────────────────────────────────────────────────

    /// <summary>
    /// Reads the current desktop state, assigns icons to containers via saved order,
    /// then starts the file-system watcher.
    /// </summary>
    public void Initialize()
    {
        var allIcons = _reader.ReadDesktopIcons();
        _exclusion.ApplyExclusion(allIcons);
        _classifier.ClassifyAll(allIcons);

        lock (_lock)
        {
            _icons.Clear();
            _containerIcons.Clear();

            foreach (var icon in allIcons)
                _icons[icon.FullPath] = icon;

            // Restore per-container assignment from saved order
            foreach (var container in _settings.Config.Containers)
            {
                var candidates = allIcons
                    .Where(i => !i.IsSystemIcon)
                    .ToList();

                var restored = _orderService.RestoreIconOrder(container.Id, candidates)
                    .Where(i => container.IconOrder
                        .Any(e => string.Equals(e.IconPath, i.FullPath, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                _containerIcons[container.Id] = restored;

                foreach (var icon in restored)
                    icon.AssignedContainerId = container.Id;
            }
        }

        _watcher.DesktopChanged += OnDesktopChanged;
        _watcher.Start();

        Debug.WriteLine($"[F-017] Initialized: {allIcons.Count} icons, {_containerIcons.Count} containers");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.DesktopChanged -= OnDesktopChanged;
    }

    // ── Event dispatch ────────────────────────────────────────────

    private void OnDesktopChanged(object? sender, DesktopChangeEventArgs e)
    {
        try
        {
            switch (e.ChangeType)
            {
                case DesktopChangeType.Created:
                    HandleCreated(e.FullPath);
                    break;
                case DesktopChangeType.Deleted:
                    HandleDeleted(e.FullPath);
                    break;
                case DesktopChangeType.Renamed when e.OldFullPath is not null:
                    HandleRenamed(e.OldFullPath, e.FullPath);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[F-017] Error handling {e.ChangeType} '{e.FullPath}': {ex.Message}");
        }
    }

    // ── F-017: Created ────────────────────────────────────────────

    private void HandleCreated(string fullPath)
    {
        // Retry up to 3 times — the file may be locked immediately after creation (F-017 spec)
        IconInfo? icon = null;
        for (int attempt = 0; attempt < 3 && icon is null; attempt++)
        {
            if (attempt > 0) Thread.Sleep(200);
            icon = _reader.BuildSingleIcon(fullPath);
        }

        if (icon is null)
        {
            Debug.WriteLine($"[F-017] Created: could not read '{fullPath}' after 3 attempts");
            return;
        }

        icon.IsSystemIcon = _exclusion.IsExcluded(icon);
        if (icon.IsSystemIcon)
        {
            Debug.WriteLine($"[F-017] Created: '{icon.FileName}' is a system icon — skipped");
            return;
        }

        icon.Category = _classifier.Classify(icon);

        var rule = _rules.FindMatchingRule(icon);
        if (rule is null)
        {
            Debug.WriteLine($"[F-017] Created: '{icon.FileName}' — no matching rule, position unchanged");
            lock (_lock) _icons[fullPath] = icon;
            return;
        }

        var container = _settings.Config.Containers.FirstOrDefault(c => c.Id == rule.TargetContainerId);
        if (container is null)
        {
            Debug.WriteLine($"[F-017] Created: rule '{rule.Name}' target container not found — skipped");
            lock (_lock) _icons[fullPath] = icon;
            return;
        }

        PlaceIconInContainer(icon, container);
    }

    // ── F-017: Deleted ────────────────────────────────────────────

    private void HandleDeleted(string fullPath)
    {
        lock (_lock)
        {
            if (!_icons.TryGetValue(fullPath, out var icon)) return;
            _icons.Remove(fullPath);

            if (icon.AssignedContainerId.HasValue && icon.AssignedContainerId != Guid.Empty &&
                _containerIcons.TryGetValue(icon.AssignedContainerId.Value, out var list))
            {
                list.Remove(icon);
                Debug.WriteLine($"[F-017] Deleted: '{icon.FileName}' removed from container {icon.AssignedContainerId}");
            }
            else
            {
                Debug.WriteLine($"[F-017] Deleted: '{icon.FileName}' removed from registry");
            }
        }
    }

    // ── F-017: Renamed ────────────────────────────────────────────

    private void HandleRenamed(string oldFullPath, string newFullPath)
    {
        lock (_lock)
        {
            if (!_icons.TryGetValue(oldFullPath, out var icon))
            {
                // Unknown file renamed onto desktop — treat as creation
                HandleCreated(newFullPath);
                return;
            }

            _icons.Remove(oldFullPath);

            // Update icon metadata
            icon.FileName  = Path.GetFileName(newFullPath);
            icon.FullPath  = newFullPath;
            icon.Extension = Directory.Exists(newFullPath)
                ? string.Empty
                : Path.GetExtension(newFullPath).ToLowerInvariant();
            icon.Category  = _classifier.Classify(icon);

            _icons[newFullPath] = icon;
        }

        // Re-evaluate rule (may move to a different container)
        var rule = _rules.FindMatchingRule(GetIcon(newFullPath));
        if (rule is null)
        {
            Debug.WriteLine($"[F-017] Renamed: '{Path.GetFileName(newFullPath)}' — no matching rule");
            return;
        }

        var container = _settings.Config.Containers.FirstOrDefault(c => c.Id == rule.TargetContainerId);
        if (container is null) return;

        var icon2 = GetIcon(newFullPath);

        // Remove from old container if different
        if (icon2.AssignedContainerId.HasValue && icon2.AssignedContainerId != Guid.Empty &&
            icon2.AssignedContainerId != container.Id)
        {
            lock (_lock)
            {
                if (_containerIcons.TryGetValue(icon2.AssignedContainerId.Value, out var oldList))
                    oldList.Remove(icon2);
                icon2.AssignedContainerId = null;
            }
        }

        PlaceIconInContainer(icon2, container);
    }

    // ── Placement helper ──────────────────────────────────────────

    /// <summary>
    /// Adds <paramref name="icon"/> to <paramref name="container"/>, re-sorts the container,
    /// moves the physical desktop icon, and persists the new order.
    /// </summary>
    private void PlaceIconInContainer(IconInfo icon, Container container)
    {
        List<IconInfo> iconList;
        lock (_lock)
        {
            if (!_containerIcons.TryGetValue(container.Id, out iconList!))
            {
                iconList = new List<IconInfo>();
                _containerIcons[container.Id] = iconList;
            }

            if (!iconList.Contains(icon))
                iconList.Add(icon);

            icon.AssignedContainerId = container.Id;
            _icons[icon.FullPath] = icon;
        }

        // Sort + compute new positions (pure logic, no I/O)
        var sorted = _sortService.SortAndComputePositions(container, iconList);

        // Build display-name → position map for Win32 call
        var positions = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
        foreach (var ic in sorted)
        {
            var displayName = ic.Extension == ".lnk"
                ? Path.GetFileNameWithoutExtension(ic.FileName)
                : ic.FileName;
            positions[displayName] = (ic.X, ic.Y);
        }

        WritePositions(positions);

        _orderService.SaveIconOrder(container.Id, sorted);

        Debug.WriteLine($"[F-017] '{icon.FileName}' → container '{container.Name}' (rule matched)");
    }

    /// <summary>
    /// Writes icon positions to the desktop. Extracted for testability.
    /// </summary>
    protected virtual void WritePositions(Dictionary<string, (int X, int Y)> positions)
        => DesktopIconInterop.WriteIconPositions(positions);

    // ── Internal helpers ──────────────────────────────────────────

    private IconInfo GetIcon(string fullPath)
    {
        lock (_lock) { return _icons.TryGetValue(fullPath, out var icon) ? icon : new IconInfo { FullPath = fullPath }; }
    }

    /// <summary>Exposes internal icon registry for unit testing.</summary>
    internal IReadOnlyDictionary<string, IconInfo> Icons
    {
        get { lock (_lock) { return new Dictionary<string, IconInfo>(_icons, StringComparer.OrdinalIgnoreCase); } }
    }

    internal IReadOnlyList<IconInfo> GetContainerIcons(Guid containerId)
    {
        lock (_lock)
        {
            return _containerIcons.TryGetValue(containerId, out var list)
                ? list.ToList()
                : Array.Empty<IconInfo>();
        }
    }

    /// <summary>Seeds the icon registry directly — for unit testing only.</summary>
    internal void SeedIcons(IEnumerable<IconInfo> icons)
    {
        lock (_lock)
        {
            _icons.Clear();
            foreach (var icon in icons)
                _icons[icon.FullPath] = icon;
        }
    }

    /// <summary>Seeds the per-container icon list directly — for unit testing only.</summary>
    internal void SeedContainerIcons(Guid containerId, IEnumerable<IconInfo> icons)
    {
        lock (_lock)
        {
            _containerIcons[containerId] = icons.ToList();
        }
    }

    /// <summary>Directly dispatches a change event — for unit testing only.</summary>
    internal void ProcessChangeEvent(DesktopChangeEventArgs e) => OnDesktopChanged(null, e);
}
