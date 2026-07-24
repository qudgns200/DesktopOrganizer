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

        // F-017 "규칙 우선성": a container's saved IconOrder is a display-order cache,
        // not a source of truth for membership — re-validate every restored placement
        // against the CURRENTLY active rules so items whose matching rule was since
        // deleted/edited/disabled don't stay stuck in a container forever.
        UnassignNonMatchingIcons(ComputeMatchedContainers(allIcons));

        _watcher.DesktopChanged += OnDesktopChanged;
        if (_settings.Config.Settings.WatcherEnabled)
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

        // Single pass over the fresh read determines, per icon, the one container (if
        // any) it currently matches — the sole source of truth for both (a) placing
        // newly-matching icons below and (b) unassigning icons that no longer belong
        // where they're currently tracked (F-017 "규칙 우선성").
        var matchedContainerByPath = ComputeMatchedContainers(allIcons);

        // Must run BEFORE placement: otherwise a rule that now points an icon at a
        // DIFFERENT container than before would leave a stale duplicate behind in
        // its old one, since placement only ever adds/updates, never removes.
        UnassignNonMatchingIcons(matchedContainerByPath);

        // Group matching icons per container (avoids N separate Win32 calls)
        var containerGroups = new Dictionary<Guid, (Container Container, List<IconInfo> NewIcons)>();
        int matched = 0;

        foreach (var icon in allIcons)
        {
            if (!matchedContainerByPath.TryGetValue(icon.FullPath, out var containerId)) continue;
            var container = _settings.Config.Containers.First(c => c.Id == containerId);

            if (!containerGroups.TryGetValue(containerId, out var group))
            {
                group = (container, new List<IconInfo>());
                containerGroups[containerId] = group;
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

    // ── F-017 "규칙 우선성": container membership resync ───────────

    /// <summary>
    /// Computes, for every non-system icon in <paramref name="freshIcons"/>, the single
    /// container (if any) it currently matches under the active Rules (First-Match,
    /// disabled rules skipped — same semantics as <see cref="RuleService.FindMatchingRule"/>).
    /// This is the sole source of truth for container membership: used both to place
    /// newly-matching icons and, in <see cref="UnassignNonMatchingIcons"/>, to unassign
    /// icons that no longer belong where they're currently tracked.
    /// Internal (not private) so DesktopOrganizer.Tests can exercise it directly with a
    /// controlled icon list — <see cref="Initialize"/>/<see cref="ApplyAllRules"/> read the
    /// real OS desktop via <see cref="DesktopReaderService"/>, which isn't swappable in tests.
    /// </summary>
    internal Dictionary<string, Guid> ComputeMatchedContainers(IEnumerable<IconInfo> freshIcons)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var icon in freshIcons)
        {
            if (icon.IsSystemIcon) continue;
            var rule = _rules.FindMatchingRule(icon);
            if (rule is null) continue;
            var container = _settings.Config.Containers.FirstOrDefault(c => c.Id == rule.TargetContainerId);
            if (container is null) continue;
            result[icon.FullPath] = container.Id;
        }
        return result;
    }

    /// <summary>
    /// Removes any currently-tracked container icon whose path is not mapped to that
    /// exact container in <paramref name="matchedContainerByPath"/> — i.e. it no longer
    /// matches any active rule for the container it's sitting in (rule deleted, edited,
    /// disabled, or the icon itself was renamed/reclassified/deleted). Without this,
    /// a placement made under since-changed rules would stay in its container forever,
    /// since both <see cref="Initialize"/> (restores from saved order) and the
    /// placement loop above (adds/updates only) never revisit it otherwise.
    /// Only container TRACKING changes — the real file and its current on-screen
    /// coordinates are left untouched (non-destructive; F-007 spec). Persists the
    /// updated (possibly empty) order for every container whose list changed, so the
    /// removal survives restart instead of being silently re-restored from stale data.
    /// Internal (not private) — see <see cref="ComputeMatchedContainers"/> for why.
    /// </summary>
    internal void UnassignNonMatchingIcons(Dictionary<string, Guid> matchedContainerByPath)
    {
        var changedContainerIds = new List<Guid>();

        lock (_lock)
        {
            foreach (var (containerId, iconList) in _containerIcons)
            {
                bool changed = false;
                for (int i = iconList.Count - 1; i >= 0; i--)
                {
                    var icon = iconList[i];
                    if (matchedContainerByPath.TryGetValue(icon.FullPath, out var targetId) && targetId == containerId)
                        continue; // still correctly assigned here

                    iconList.RemoveAt(i);
                    icon.AssignedContainerId = null;
                    changed = true;
                }
                if (changed) changedContainerIds.Add(containerId);
            }
        }

        foreach (var containerId in changedContainerIds)
        {
            List<IconInfo> remaining;
            lock (_lock) { remaining = _containerIcons[containerId].ToList(); }
            _orderService.SaveIconOrder(containerId, remaining);
        }

        if (changedContainerIds.Count > 0)
            LogService.Instance.Info("F-017",
                $"{changedContainerIds.Count} container(s) had icon(s) unassigned — no longer match any active rule");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.DesktopChanged -= OnDesktopChanged;
    }

    /// <summary>
    /// Re-applies settings that services otherwise only read once at construction
    /// (F-023): excluded paths, the watcher debounce window, and the watcher
    /// enabled/disabled state (unified with the tray "감시 일시정지/재개" toggle —
    /// both read/write the same AppSettings.WatcherEnabled). Call after the Settings
    /// dialog saves changes, or after the tray toggle, so they take effect without
    /// an app restart.
    /// </summary>
    public void ApplySettingsChanged()
    {
        _exclusion.UpdateExcludedPaths(_settings.Config.Settings.ExcludedPaths);
        _watcher.DebounceMs = _settings.Config.Settings.WatcherDebounceMs;

        if (_settings.Config.Settings.WatcherEnabled)
            _watcher.Start();
        else
            _watcher.Stop();
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
    /// Finds the icon located at <paramref name="localX"/>/<paramref name="localY"/>
    /// (container-local coordinates) without launching it — lets the caller (UI layer)
    /// inspect the icon first, e.g. to decide whether F-025's external-link confirmation
    /// applies, before committing to <see cref="LaunchIcon"/>.
    /// </summary>
    public IconInfo? FindIconAt(Guid containerId, double localX, double localY)
    {
        var container = _settings.Config.Containers.FirstOrDefault(c => c.Id == containerId);
        if (container is null) return null;

        List<IconInfo> ordered;
        lock (_lock)
        {
            if (!_containerIcons.TryGetValue(containerId, out var list) || list.Count == 0) return null;
            // Display order must match ComputePositions (sort, then grid layout).
            ordered = _sortService.Sort(list, container.SortMode).ToList();
        }

        int? idx = _sortService.HitTestIndex(container, localX, localY, ordered.Count);
        return idx is null ? null : ordered[idx.Value];
    }

    /// <summary>Opens <paramref name="icon"/> with the shell. Only OPENS the file; never moves/copies/deletes it.</summary>
    public void LaunchIcon(IconInfo icon) => LaunchFile(icon.FullPath);

    /// <summary>
    /// Finds and immediately opens the icon at the given container-local coordinates —
    /// the double-click "launch" action for icons managed inside a container, with no
    /// confirmation gate. UI code that needs to confirm first (F-025) should call
    /// <see cref="FindIconAt"/> and <see cref="LaunchIcon"/> separately instead.
    /// </summary>
    public void LaunchIconInContainer(Guid containerId, double localX, double localY)
    {
        var icon = FindIconAt(containerId, localX, localY);
        if (icon is not null) LaunchIcon(icon);
    }

    /// <summary>Opens a file with its default shell handler. Extracted for testability.</summary>
    protected virtual void LaunchFile(string fullPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = fullPath, UseShellExecute = true });
            LogService.Instance.Info("Launch", $"Opened '{Path.GetFileName(fullPath)}'");
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("Launch", $"Failed to open '{fullPath}': {ex.Message}");
        }
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
