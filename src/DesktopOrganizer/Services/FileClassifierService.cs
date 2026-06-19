using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

/// <summary>
/// F-002: Classifies each desktop icon into one of 9 categories based on file extension.
/// The extension map is defined here and can be extended without changing callers.
/// </summary>
public class FileClassifierService
{
    private static readonly Dictionary<string, FileCategory> ExtensionMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Document
            { ".pdf",  FileCategory.Document },
            { ".doc",  FileCategory.Document },
            { ".docx", FileCategory.Document },
            { ".xls",  FileCategory.Document },
            { ".xlsx", FileCategory.Document },
            { ".ppt",  FileCategory.Document },
            { ".pptx", FileCategory.Document },
            { ".txt",  FileCategory.Document },
            { ".hwp",  FileCategory.Document },
            { ".hwpx", FileCategory.Document },
            { ".rtf",  FileCategory.Document },
            { ".odt",  FileCategory.Document },
            { ".ods",  FileCategory.Document },
            { ".odp",  FileCategory.Document },
            { ".csv",  FileCategory.Document },
            { ".md",   FileCategory.Document },

            // Image
            { ".jpg",  FileCategory.Image },
            { ".jpeg", FileCategory.Image },
            { ".png",  FileCategory.Image },
            { ".gif",  FileCategory.Image },
            { ".bmp",  FileCategory.Image },
            { ".webp", FileCategory.Image },
            { ".svg",  FileCategory.Image },
            { ".psd",  FileCategory.Image },
            { ".ai",   FileCategory.Image },
            { ".ico",  FileCategory.Image },
            { ".tiff", FileCategory.Image },
            { ".tif",  FileCategory.Image },
            { ".heic", FileCategory.Image },
            { ".raw",  FileCategory.Image },

            // Video
            { ".mp4",  FileCategory.Video },
            { ".avi",  FileCategory.Video },
            { ".mov",  FileCategory.Video },
            { ".mkv",  FileCategory.Video },
            { ".wmv",  FileCategory.Video },
            { ".flv",  FileCategory.Video },
            { ".webm", FileCategory.Video },
            { ".m4v",  FileCategory.Video },
            { ".3gp",  FileCategory.Video },
            { ".ts",   FileCategory.Video },

            // Audio
            { ".mp3",  FileCategory.Audio },
            { ".wav",  FileCategory.Audio },
            { ".flac", FileCategory.Audio },
            { ".aac",  FileCategory.Audio },
            { ".ogg",  FileCategory.Audio },
            { ".m4a",  FileCategory.Audio },
            { ".wma",  FileCategory.Audio },
            { ".opus", FileCategory.Audio },

            // Archive
            { ".zip",  FileCategory.Archive },
            { ".rar",  FileCategory.Archive },
            { ".7z",   FileCategory.Archive },
            { ".tar",  FileCategory.Archive },
            { ".gz",   FileCategory.Archive },
            { ".bz2",  FileCategory.Archive },
            { ".xz",   FileCategory.Archive },
            { ".cab",  FileCategory.Archive },

            // Executable
            { ".exe",  FileCategory.Executable },
            { ".msi",  FileCategory.Executable },
            { ".bat",  FileCategory.Executable },
            { ".cmd",  FileCategory.Executable },
            { ".ps1",  FileCategory.Executable },
            { ".vbs",  FileCategory.Executable },
            { ".com",  FileCategory.Executable },

            // Shortcut
            { ".lnk",  FileCategory.Shortcut },
            { ".url",  FileCategory.Shortcut },
        };

    public FileCategory Classify(IconInfo icon)
    {
        if (icon.IconType == IconType.Folder)
            return FileCategory.Folder;

        if (icon.IconType == IconType.Shortcut)
            return FileCategory.Shortcut;

        if (string.IsNullOrEmpty(icon.Extension))
            return FileCategory.Other;

        return ExtensionMap.TryGetValue(icon.Extension, out var cat) ? cat : FileCategory.Other;
    }

    public void ClassifyAll(IEnumerable<IconInfo> icons)
    {
        foreach (var icon in icons)
            icon.Category = Classify(icon);
    }
}
