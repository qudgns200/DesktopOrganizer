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

        LogService.Instance.Info("F-017", $"Initialized: {allIcons.Count} icons, {_containerIcons.Count} containers");
    }

    /// <summary>
    /// Applies all active rules to every current desktop icon.
    /// Groups matching icons by container, then repositions each container in one batch.
    /// Returns the number of icons actually repositioned on the desktop (Win32 success count).
    /// </summary>
    public int ApplyAllRules()
    {
        var allIcons = _reader.ReadDesktopIcons();
        _exclusion.ApplyExclusion(allIcons);
        _classifier.ClassifyAll(allIcons);

        lock (_lock)
        {
            foreach (var icon in allIcons)
                _icons[icon.FullPath] = icon;
        }

        // Group matching icons per container (avoids N separate Win32 calls)
        var containerGroups = new Dictionary<Guid, (Container Container, List<IconInfo> NewIcons)>();
        int matched = 0;

        foreach (var icon in allIcons)
        {
            if (icon.IsSystemIcon) continue;
            var rule = _rules.FindMatchingRule(icon);
            if (rule is null) continue;
            var container = _settings.Config.Containers
                .FirstOrDefault(c => c.Id == rule.TargetContainerId);
            if (container is null) continue;

            if (!containerGroups.TryGetValue(container.Id, out var group))
            {
                group = (container, new List<IconInfo>());
                containerGroups[container.Id] = group;
            }
            group.NewIcons.Add(icon);
            matched++;
        }

        int totalMoved = 0;

        foreach (var (containerId, (container, newIcons)) in containerGroups)
        {
            List<IconInfo> iconList;
            lock (_lock)
            {
                if (!_containerIcons.TryGetValue(containerId, out iconList!))
                {
                    iconList = new List<IconInfo>();
                    _containerIcons[containerId] = iconList;
                }

                foreach (var icon in newIcons)
                {
                    int idx = iconList.FindIndex(i =>
                        string.Equals(i.FullPath, icon.FullPath, StringComparison.OrdinalIgnoreCase));
                    if (idx < 0)
                        iconList.Add(icon);
                    else
                        iconList[idx] = icon;

                    icon.AssignedContainerId = containerId;
                    _icons[icon.FullPath] = icon;
                }
            }

            var sorted = _sortService.SortAndComputePositions(container, iconList);
            var positions = BuildPositionDict(sorted);
            int moved = WritePositions(positions);
            totalMoved += moved;

            _orderService.SaveIconOrder(containerId, sorted);

            LogService.Instance.Info("F-017",
                $"ApplyAllRules: container '{container.Name}' — {newIcons.Count} matched, {moved} repositioned on desktop");
        }

        LogService.Instance.Info("F-017",
            $"ApplyAllRules complete: {matched}/{allIcons.Count} icons matched rules, {totalMoved} actually repositioned");
        return totalMoved;
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
            LogService.Instance.Error("F-017", $"Error handling {e.ChangeType} '{e.FullPath}': {ex.Message}");
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
            LogService.Instance.Warn("F-017", $"Created: could not read '{fullPath}' after 3 attempts");
            return;
        }

        icon.IsSystemIcon = _exclusion.IsExcluded(icon);
        if (icon.IsSystemIcon)
        {
            LogService.Instance.Debug("F-017", $"Created: '{icon.FileName}' is a system icon — skipped");
            return;
        }

        icon.Category = _classifier.Classify(icon);

        var rule = _rules.FindMatchingRule(icon);
        if (rule is null)
        {
            LogService.Instance.Debug("F-017", $"Created: '{icon.FileName}' — no matching rule, position unchanged");
            lock (_lock) _icons[fullPath] = icon;
            return;
        }

        var container = _settings.Config.Containers.FirstOrDefault(c => c.Id == rule.TargetContainerId);
        if (container is null)
        {
            LogService.Instance.Warn("F-017", $"Created: rule '{rule.Name}' target container not found — skipped");
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
                LogService.Instance.Info("F-017", $"Deleted: '{icon.FileName}' removed from container {icon.AssignedContainerId}");
            }
            else
            {
                LogService.Instance.Debug("F-017", $"Deleted: '{icon.FileName}' removed from registry");
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
            LogService.Instance.Debug("F-017", $"Renamed: '{Path.GetFileName(newFullPath)}' — no matching rule");
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

            // Use path-based deduplication — Contains uses reference equality which
            // fails when ApplyAllRules creates fresh IconInfo objects for the same file.
            int existingIdx = iconList.FindIndex(i =>
                string.Equals(i.FullPath, icon.FullPath, StringComparison.OrdinalIgnoreCase));
            if (existingIdx < 0)
                iconList.Add(icon);
            else
                iconList[existingIdx] = icon;  // refresh metadata

            icon.AssignedContainerId = container.Id;
            _icons[icon.FullPath] = icon;
        }

        // Sort + compute new positions (pure logic, no I/O)
        var sorted = _sortService.SortAndComputePositions(container, iconList);
        WritePositions(BuildPositionDict(sorted));
        _orderService.SaveIconOrder(container.Id, sorted);

        LogService.Instance.Info("F-017", $"'{icon.FileName}' → container '{container.Name}' (rule matched)");
    }

    /// <summary>Repositions all icons assigned to <paramref name="containerId"/> after a container move/resize.</summary>
    public void RepositionContainerIcons(Guid containerId)
    {
        var container = _settings.Config.Containers.FirstOrDefault(c => c.Id == containerId);
        if (container is null) return;

        List<IconInfo> snapshot;
        lock (_lock)
        {
            if (!_containerIcons.TryGetValue(containerId, out var list) || list.Count == 0)
                return;
            snapshot = list.ToList();
        }

        var sorted = _sortService.SortAndComputePositions(container, snapshot);
        int moved = WritePositions(BuildPositionDict(sorted));

        // Persist updated positions back into the live list
        lock (_lock)
        {
            if (_containerIcons.TryGetValue(containerId, out var live))
            {
                foreach (var ic in sorted)
                {
                    int idx = live.FindIndex(i =>
                        string.Equals(i.FullPath, ic.FullPath, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) { live[idx].X = ic.X; live[idx].Y = ic.Y; }
                }
            }
        }

        LogService.Instance.Info("F-007",
            $"RepositionContainerIcons '{container.Name}': {moved}/{snapshot.Count} repositioned");
    }

    /// <summary>
    /// Writes icon positions to the desktop. Extracted for testability.
    /// Returns the number of icons actually moved by Win32.
    /// </summary>
    protected virtual int WritePositions(Dictionary<string, (int X, int Y)> positions)
        => DesktopIconInterop.WriteIconPositions(positions);

    /// <summary>Builds a display-name → (X,Y) map from a sorted icon list.</summary>
    private static Dictionary<string, (int X, int Y)> BuildPositionDict(IList<IconInfo> sorted)
    {
        var positions = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
        foreach (var ic in sorted)
        {
            var displayName = ic.Extension == ".lnk"
                ? Path.GetFileNameWithoutExtension(ic.FileName)
                : ic.FileName;
            positions[displayName] = (ic.X, ic.Y);
        }
        return positions;
    }

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
