using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-004 Container 생성 / F-005 Container 수정 / F-006 Container 삭제
/// Delegates persistence to SettingsService after every mutating operation.
/// </summary>
public class ContainerService
{
    private readonly SettingsService _settings;

    public ContainerService(SettingsService settings) => _settings = settings;

    /// <summary>All containers currently in memory (loaded from config).</summary>
    public IReadOnlyList<Container> GetAll() => _settings.Config.Containers;

    // ── F-004 ────────────────────────────────────────────────────

    /// <summary>Creates a new container at the given overlay coordinates.</summary>
    public Container Create(double x, double y)
    {
        int n = _settings.Config.Containers.Count + 1;
        var container = new Container
        {
            Name = $"새 Container {n}",
            X    = x,
            Y    = y
        };
        _settings.Config.Containers.Add(container);
        _settings.Save();
        return container;
    }

    // ── F-005 ────────────────────────────────────────────────────

    /// <summary>
    /// Renames a container.  Returns false (and does NOT save) if the new name is blank
    /// or the container ID is not found.
    /// </summary>
    public bool Rename(Guid id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        var c = Find(id);
        if (c is null) return false;

        c.Name      = newName.Trim();
        c.UpdatedAt = DateTime.UtcNow;
        _settings.Save();
        return true;
    }

    /// <summary>Changes the sort mode for a container.</summary>
    public bool SetSortMode(Guid id, SortMode mode)
    {
        var c = Find(id);
        if (c is null) return false;

        c.SortMode  = mode;
        c.UpdatedAt = DateTime.UtcNow;
        _settings.Save();
        return true;
    }

    // ── F-006 ────────────────────────────────────────────────────

    /// <summary>
    /// Deletes a container (does NOT delete or move any actual files).
    /// Returns false if the container is not found.
    /// </summary>
    public bool Delete(Guid id)
    {
        var c = Find(id);
        if (c is null) return false;

        _settings.Config.Containers.Remove(c);
        _settings.Save();
        return true;
    }

    // ── F-007 ────────────────────────────────────────────────────

    /// <summary>Persists the container's updated position.  Call once on drag-end.</summary>
    public void UpdatePosition(Guid id, double x, double y)
    {
        var c = Find(id);
        if (c is null) return;
        c.X         = x;
        c.Y         = y;
        c.UpdatedAt = DateTime.UtcNow;
        _settings.Save();
    }

    // ── F-008 ────────────────────────────────────────────────────

    public const double MinContainerWidth  = 120;
    public const double MinContainerHeight = 80;

    /// <summary>
    /// Persists updated position and size.  Returns false if the new dimensions are
    /// below the minimum; in that case nothing is saved.
    /// </summary>
    public bool Resize(Guid id, double x, double y, double width, double height)
    {
        if (width < MinContainerWidth || height < MinContainerHeight) return false;
        var c = Find(id);
        if (c is null) return false;
        c.X         = x;
        c.Y         = y;
        c.Width     = width;
        c.Height    = height;
        c.UpdatedAt = DateTime.UtcNow;
        _settings.Save();
        return true;
    }

    // ── F-009 ────────────────────────────────────────────────────

    /// <summary>Replaces the container's style and saves.</summary>
    public void UpdateStyle(Guid id, ContainerStyle style)
    {
        var c = Find(id);
        if (c is null) return;
        c.Style     = style;
        c.UpdatedAt = DateTime.UtcNow;
        _settings.Save();
    }

    // ── Helpers ──────────────────────────────────────────────────

    private Container? Find(Guid id) =>
        _settings.Config.Containers.FirstOrDefault(c => c.Id == id);
}
