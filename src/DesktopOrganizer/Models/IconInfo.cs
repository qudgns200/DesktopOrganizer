namespace DesktopOrganizer.Models;

public enum IconType
{
    File,
    Folder,
    Shortcut,
    SystemItem
}

public enum FileCategory
{
    Document,
    Image,
    Video,
    Audio,
    Archive,
    Executable,
    Shortcut,
    Folder,
    Other
}

public class IconInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public IconType IconType { get; set; }
    public FileCategory Category { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Extension { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsSystemIcon { get; set; }
    public Guid? AssignedContainerId { get; set; }
    public int OrderIndex { get; set; } = -1;
}
