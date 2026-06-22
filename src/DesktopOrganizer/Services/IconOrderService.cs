using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-011: Persists and restores per-Container icon order in config.json.
/// </summary>
public class IconOrderService
{
    private readonly SettingsService _settings;

    public IconOrderService(SettingsService settings) => _settings = settings;

    // ── F-011: Save ───────────────────────────────────────────────

    /// <summary>
    /// Saves the current icon order for a container.
    /// Overwrites any previously saved order for the same container.
    /// </summary>
    public void SaveIconOrder(Guid containerId, IEnumerable<IconInfo> icons)
    {
        var container = FindContainer(containerId);
        if (container is null) return;

        var now    = DateTime.UtcNow;
        var entries = icons.Select((icon, idx) => new IconOrderEntry
        {
            IconPath   = icon.FullPath,
            OrderIndex = idx,
            SavedAt    = now
        }).ToList();

        container.IconOrder = entries;
        container.UpdatedAt = now;
        _settings.Save();
    }

    // ── F-011: Restore ────────────────────────────────────────────

    /// <summary>
    /// Returns <paramref name="icons"/> reordered to match the saved sequence.
    /// Icons whose paths are not in the saved order are appended at the end.
    /// Sets <see cref="IconInfo.OrderIndex"/> on every icon.
    /// Stale saved entries (paths not present in <paramref name="icons"/>) are silently skipped.
    /// </summary>
    public IList<IconInfo> RestoreIconOrder(Guid containerId, IList<IconInfo> icons)
    {
        var container = FindContainer(containerId);
        if (container is null || container.IconOrder.Count == 0)
            return AssignIndicesAndReturn(icons);

        var savedIndex = container.IconOrder
            .ToDictionary(e => e.IconPath, e => e.OrderIndex, StringComparer.OrdinalIgnoreCase);

        var positioned   = new List<IconInfo>();
        var unpositioned = new List<IconInfo>();

        foreach (var icon in icons)
        {
            if (savedIndex.ContainsKey(icon.FullPath))
                positioned.Add(icon);
            else
                unpositioned.Add(icon);
        }

        positioned.Sort((a, b) =>
            savedIndex[a.FullPath].CompareTo(savedIndex[b.FullPath]));

        var result = positioned.Concat(unpositioned).ToList();
        return AssignIndicesAndReturn(result);
    }

    // ── F-011: Cleanup ────────────────────────────────────────────

    /// <summary>
    /// Removes saved entries for icons that no longer exist on disk, then saves.
    /// </summary>
    public void CleanupStaleEntries(Guid containerId, IEnumerable<string> existingPaths)
    {
        var container = FindContainer(containerId);
        if (container is null || container.IconOrder.Count == 0) return;

        var existing = new HashSet<string>(existingPaths, StringComparer.OrdinalIgnoreCase);
        int removed  = container.IconOrder.RemoveAll(e => !existing.Contains(e.IconPath));

        if (removed > 0)
        {
            container.UpdatedAt = DateTime.UtcNow;
            _settings.Save();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────

    private Container? FindContainer(Guid id) =>
        _settings.Config.Containers.FirstOrDefault(c => c.Id == id);

    private static IList<IconInfo> AssignIndicesAndReturn(IList<IconInfo> icons)
    {
        for (int i = 0; i < icons.Count; i++)
            icons[i].OrderIndex = i;
        return icons;
    }
}
