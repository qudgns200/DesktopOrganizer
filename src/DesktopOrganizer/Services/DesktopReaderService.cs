using System.Diagnostics;
using DesktopOrganizer.Interop;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-001: Reads all icons from the user and public desktop directories.
/// Collects 8 attributes per icon: name, path, position, type, extension, created, modified, system flag.
/// Does NOT move, copy, or delete any files.
/// </summary>
public class DesktopReaderService
{
    // Metadata file that appears in every directory — never expose it as an icon
    private const string DesktopIni = "desktop.ini";

    public List<IconInfo> ReadDesktopIcons()
    {
        var result = new List<IconInfo>();
        var desktopPaths = GetDesktopPaths();

        if (desktopPaths.Count == 0)
        {
            Debug.WriteLine("[F-001] No desktop paths resolved");
            return result;
        }

        // Get icon positions once (cross-process read); fall back to (0,0) on failure
        Dictionary<string, (int X, int Y)> positions;
        try
        {
            positions = DesktopIconInterop.ReadIconPositions();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[F-001] ReadIconPositions failed — using (0,0) fallback: {ex.Message}");
            positions = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
        }

        // Enumerate both desktop roots; deduplicate by full path
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in desktopPaths)
        {
            if (!Directory.Exists(root)) continue;

            foreach (var entryPath in Directory.EnumerateFileSystemEntries(root))
            {
                if (!seen.Add(entryPath)) continue;

                try
                {
                    var icon = BuildIconInfo(entryPath, positions);
                    if (icon is not null) result.Add(icon);
                }
                catch (Exception ex)
                {
                    // Individual item failure must not stop the rest (F-001 acceptance criteria)
                    Debug.WriteLine($"[F-001] Skipped '{entryPath}': {ex.Message}");
                }
            }
        }

        Debug.WriteLine($"[F-001] Collected {result.Count} icons from desktop");
        return result;
    }

    private static List<string> GetDesktopPaths()
    {
        var paths = new List<string>();

        var user = ShellApi.GetUserDesktopPath();
        if (!string.IsNullOrEmpty(user)) paths.Add(user);

        var pub = ShellApi.GetPublicDesktopPath();
        if (!string.IsNullOrEmpty(pub) &&
            !paths.Contains(pub, StringComparer.OrdinalIgnoreCase))
            paths.Add(pub);

        return paths;
    }

    private static IconInfo? BuildIconInfo(string fullPath, Dictionary<string, (int X, int Y)> positions)
    {
        var fileName = Path.GetFileName(fullPath);

        if (string.Equals(fileName, DesktopIni, StringComparison.OrdinalIgnoreCase))
            return null;

        bool isDir = Directory.Exists(fullPath);
        var ext = isDir ? string.Empty : Path.GetExtension(fullPath).ToLowerInvariant();

        DateTime createdAt, modifiedAt;
        if (isDir)
        {
            var di = new DirectoryInfo(fullPath);
            createdAt = di.CreationTimeUtc;
            modifiedAt = di.LastWriteTimeUtc;
        }
        else
        {
            var fi = new FileInfo(fullPath);
            createdAt = fi.CreationTimeUtc;
            modifiedAt = fi.LastWriteTimeUtc;
        }

        var iconType = isDir
            ? IconType.Folder
            : ext == ".lnk" ? IconType.Shortcut : IconType.File;

        // Shortcuts appear in the ListView without their .lnk extension
        var displayName = ext == ".lnk"
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;

        if (!positions.TryGetValue(displayName, out var pos))
            positions.TryGetValue(fileName, out pos);

        return new IconInfo
        {
            FileName = fileName,
            FullPath = fullPath,
            IconType = iconType,
            Extension = ext,
            CreatedAt = createdAt,
            ModifiedAt = modifiedAt,
            IsSystemIcon = false,   // set by ExclusionService
            X = pos.X,
            Y = pos.Y
        };
    }
}
