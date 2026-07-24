using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-010: Sorts icons by 9 criteria and computes their absolute grid positions
/// inside a Container.  Pure logic — no I/O, no Win32 calls.
/// </summary>
public class IconSortService
{
    // Base grid cell size (pixels at 100% DPI). The vertical cell is taller than the
    // horizontal one because a desktop icon reserves space for its (often two-line) label.
    // These are deliberately >= the Windows desktop grid pitch so that, when the shell's
    // "Align icons to grid" option is active, no two icons snap into the same cell.
    public const int IconCellWidth  = 75;
    public const int IconCellHeight = 90;

    // Inset from the container edge
    public const int PaddingX = 10;
    public const int PaddingY = 10;

    // Space reserved for the title bar when ShowTitle=true
    public const int TitleBarHeight = 26;

    // Optional: supplies the user-configured icon spacing (F-010). Null in unit tests.
    private readonly SettingsService? _settings;

    /// <summary>Parameterless ctor — spacing defaults to 0 (used by unit tests).</summary>
    public IconSortService() { }

    /// <summary>Production ctor — reads <see cref="AppSettings.IconSpacingPx"/> for grid spacing.</summary>
    public IconSortService(SettingsService settings) => _settings = settings;

    /// <summary>User-configured spacing added to each base cell (F-010 "정렬 간격").</summary>
    private int Spacing => _settings?.Config.Settings.IconSpacingPx ?? 0;

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
        // Effective cell pitch = base cell + user-configured spacing.
        int cellW = IconCellWidth  + Spacing;
        int cellH = IconCellHeight + Spacing;

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

        int cellW = IconCellWidth  + Spacing;
        int cellH = IconCellHeight + Spacing;

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
