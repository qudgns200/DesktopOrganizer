using DesktopOrganizer.Interop;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-010: Sorts icons by 9 criteria and computes their absolute grid positions
/// inside a Container.  Pure logic — no I/O, no Win32 calls.
/// </summary>
public class IconSortService
{
    // Base grid cell size (pixels at 100% DPI), used as a FLOOR. The real desktop grid
    // cell (measured via LVM_GETITEMSPACING) is used when it is larger, so that with the
    // shell's "Align icons to grid" active no two icons snap into the same cell (which the
    // shell would otherwise resolve by scattering them — the reported move-scatter bug).
    // The vertical cell is taller than the horizontal one because a desktop icon reserves
    // space for its (often two-line) label.
    public const int IconCellWidth  = 75;
    public const int IconCellHeight = 90;

    // Inset from the container edge
    public const int PaddingX = 10;
    public const int PaddingY = 10;

    // Space reserved for the title bar when ShowTitle=true
    public const int TitleBarHeight = 26;

    // Optional: supplies the user-configured icon spacing (F-010). Null in unit tests.
    private readonly SettingsService? _settings;

    // Optional: measures the real desktop grid cell size in DIPs. Null in unit tests
    // (→ base constants only). Wired to DesktopIconInterop.GetDesktopGridCellSize in production.
    private readonly Func<(double Cx, double Cy)?>? _gridCellProvider;

    /// <summary>Parameterless ctor — spacing 0, no grid measurement (used by unit tests).</summary>
    public IconSortService() { }

    /// <summary>Production ctor — reads spacing from settings and measures the real desktop grid.</summary>
    public IconSortService(SettingsService settings)
        : this(settings, DesktopIconInterop.GetDesktopGridCellSize) { }

    /// <summary>Full ctor — the grid-cell provider is injectable for unit testing.</summary>
    internal IconSortService(SettingsService? settings, Func<(double Cx, double Cy)?>? gridCellProvider)
    {
        _settings         = settings;
        _gridCellProvider = gridCellProvider;
    }

    /// <summary>User-configured spacing added to each base cell (F-010 "정렬 간격").</summary>
    private int Spacing => _settings?.Config.Settings.IconSpacingPx ?? 0;

    /// <summary>
    /// Effective per-icon cell pitch in DIPs: the larger of the base constant and the
    /// measured desktop grid cell, plus the user spacing. Re-measured each call so it stays
    /// correct across DPI / resolution changes (the underlying query is a single cheap message).
    /// </summary>
    private (int W, int H) EffectiveCell()
    {
        int w = IconCellWidth;
        int h = IconCellHeight;

        (double Cx, double Cy)? grid = null;
        try { grid = _gridCellProvider?.Invoke(); } catch { grid = null; }

        if (grid is { } g)
        {
            if (g.Cx > 0) w = Math.Max(w, (int)Math.Ceiling(g.Cx));
            if (g.Cy > 0) h = Math.Max(h, (int)Math.Ceiling(g.Cy));
        }

        return (w + Spacing, h + Spacing);
    }

    // ── F-010: Sort ───────────────────────────────────────────────

    /// <summary>
    /// Returns a new list sorted by <paramref name="mode"/>.
    /// Manual mode orders by <see cref="IconInfo.OrderIndex"/> (unset items go last).
    /// </summary>
    public IList<IconInfo> Sort(IEnumerable<IconInfo> icons, SortMode mode)
    {
        var list = icons.ToList();
        return mode switch
        {
            SortMode.NameAsc      => [.. list.OrderBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.NameDesc     => [.. list.OrderByDescending(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.Extension    => [.. list.OrderBy(i => i.Extension, StringComparer.OrdinalIgnoreCase)
                                           .ThenBy(i => i.FileName,    StringComparer.OrdinalIgnoreCase)],
            SortMode.FileType     => [.. list.OrderBy(i => i.Category)
                                           .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.CreatedAsc   => [.. list.OrderBy(i => i.CreatedAt)
                                           .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.CreatedDesc  => [.. list.OrderByDescending(i => i.CreatedAt)
                                           .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.ModifiedAsc  => [.. list.OrderBy(i => i.ModifiedAt)
                                           .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.ModifiedDesc => [.. list.OrderByDescending(i => i.ModifiedAt)
                                           .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)],
            SortMode.Manual       => [.. list.OrderBy(i => i.OrderIndex >= 0 ? i.OrderIndex : int.MaxValue)],
            _                     => list
        };
    }

    // ── F-010: Grid layout ────────────────────────────────────────

    /// <summary>
    /// Assigns absolute screen (X, Y) coordinates and <see cref="IconInfo.OrderIndex"/> to
    /// every icon in <paramref name="sortedIcons"/>, starting from the container's top-left
    /// corner with padding.  Wraps to the next row when container width is exhausted.
    /// Also sets <see cref="IconInfo.AssignedContainerId"/>.
    /// </summary>
    public void ComputePositions(Container container, IList<IconInfo> sortedIcons)
    {
        // Effective cell pitch = max(base, measured desktop grid) + user spacing.
        var (cellW, cellH) = EffectiveCell();

        double topStart = container.Y + PaddingY
            + (container.Style.ShowTitle ? TitleBarHeight : 0);

        double availableWidth = container.Width - PaddingX * 2;
        int iconsPerRow = Math.Max(1, (int)(availableWidth / cellW));

        for (int i = 0; i < sortedIcons.Count; i++)
        {
            int col = i % iconsPerRow;
            int row = i / iconsPerRow;

            sortedIcons[i].X                  = (int)(container.X + PaddingX + col * cellW);
            sortedIcons[i].Y                  = (int)(topStart              + row * cellH);
            sortedIcons[i].OrderIndex         = i;
            sortedIcons[i].AssignedContainerId = container.Id;
        }
    }

    /// <summary>Convenience: sort then compute positions in one call.</summary>
    public IList<IconInfo> SortAndComputePositions(Container container, IEnumerable<IconInfo> icons)
    {
        var sorted = Sort(icons, container.SortMode);
        ComputePositions(container, sorted);
        return sorted;
    }

    // ── Hit testing (for double-click launch) ─────────────────────

    /// <summary>
    /// Maps a point in container-local coordinates (0,0 = container top-left) to the
    /// index of the icon occupying that grid cell, using the same layout as
    /// <see cref="ComputePositions"/>.  Returns null when the point is outside the
    /// icon grid or lands on a cell with no icon.
    /// </summary>
    public int? HitTestIndex(Container container, double localX, double localY, int iconCount)
    {
        if (iconCount <= 0) return null;

        var (cellW, cellH) = EffectiveCell();

        double left = PaddingX;
        double top  = PaddingY + (container.Style.ShowTitle ? TitleBarHeight : 0);

        if (localX < left || localY < top) return null;

        double availableWidth = container.Width - PaddingX * 2;
        int iconsPerRow = Math.Max(1, (int)(availableWidth / cellW));

        int col = (int)((localX - left) / cellW);
        int row = (int)((localY - top)  / cellH);
        if (col < 0 || col >= iconsPerRow) return null;

        int index = row * iconsPerRow + col;
        return index >= 0 && index < iconCount ? index : null;
    }
}
