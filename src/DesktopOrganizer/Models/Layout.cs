namespace DesktopOrganizer.Models;

public class LayoutIconPlacement
{
    public string IconPath { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public class LayoutContainerSnapshot
{
    public Guid ContainerId { get; set; }
    public string ContainerName { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public ContainerStyle Style { get; set; } = new();
    public SortMode SortMode { get; set; }
    public List<LayoutIconPlacement> Icons { get; set; } = new();
}

public class Layout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public List<LayoutContainerSnapshot> Containers { get; set; } = new();
}
