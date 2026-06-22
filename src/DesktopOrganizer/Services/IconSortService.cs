using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-010: Sorts icons by 9 criteria and computes their absolute grid positions
/// inside a Container.  Pure logic — no I/O, no Win32 calls.
/// </summary>
public class IconSortService
{
    // Grid cell size (pixels at 100% DPI)
    public const int IconCellWidth  = 75;
    public const int IconCellHeight = 75;

    // Inset from the container edge
    public const int PaddingX = 10;
    public const int PaddingY = 10;

    // Space reserved for the title bar when ShowTitle=true
    public const int TitleBarHeight = 26;

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
        double topStart = container.Y + PaddingY
            + (container.Style.ShowTitle ? TitleBarHeight : 0);

        double availableWidth = container.Width - PaddingX * 2;
        int iconsPerRow = Math.Max(1, (int)(availableWidth / IconCellWidth));

        for (int i = 0; i < sortedIcons.Count; i++)
        {
            int col = i % iconsPerRow;
            int row = i / iconsPerRow;

            sortedIcons[i].X                  = (int)(container.X + PaddingX + col * IconCellWidth);
            sortedIcons[i].Y                  = (int)(topStart              + row * IconCellHeight);
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
}
