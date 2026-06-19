using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-003: Determines whether an icon is a Windows system icon that should be
/// excluded from automatic organisation. Checks in three layers:
/// 1. User-added path exclusions (from AppSettings).
/// 2. Physical existence — virtual shell items (Recycle Bin, This PC, etc.)
///    do not exist on the file system.
/// 3. Fallback display-name match for well-known system icons.
/// </summary>
public class ExclusionService
{
    // Known system icon display names (both Korean and English)
    private static readonly HashSet<string> SystemDisplayNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "내 PC", "This PC",
            "휴지통", "Recycle Bin",
            "내 문서", "Documents",
            "네트워크", "Network",
            "제어판", "Control Panel"
        };

    private readonly HashSet<string> _userExcludedPaths;

    public ExclusionService(AppSettings settings)
    {
        _userExcludedPaths = new HashSet<string>(
            settings.ExcludedPaths, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsExcluded(IconInfo icon)
    {
        // Layer 1: user explicitly excluded this path
        if (_userExcludedPaths.Contains(icon.FullPath))
            return true;

        // Layer 2: virtual items have no corresponding disk entry
        if (!File.Exists(icon.FullPath) && !Directory.Exists(icon.FullPath))
            return true;

        // Layer 3: display-name match (handles locale variants)
        var nameNoExt = Path.GetFileNameWithoutExtension(icon.FileName);
        if (SystemDisplayNames.Contains(nameNoExt) || SystemDisplayNames.Contains(icon.FileName))
            return true;

        return false;
    }

    /// <summary>Sets IsSystemIcon on every icon in the list.</summary>
    public void ApplyExclusion(IEnumerable<IconInfo> icons)
    {
        foreach (var icon in icons)
            icon.IsSystemIcon = IsExcluded(icon);
    }
}
