namespace DesktopOrganizer.Models;

public enum SortMode
{
    NameAsc,
    NameDesc,
    Extension,
    FileType,
    CreatedAsc,
    CreatedDesc,
    ModifiedAsc,
    ModifiedDesc,
    Manual
}

public enum BorderStyle
{
    Solid,
    Dashed,
    Dotted
}

public class ContainerStyle
{
    public string BackgroundColor { get; set; } = "#44000000";
    public double BackgroundOpacity { get; set; } = 0.8;
    public string BorderColor { get; set; } = "#CCFFFFFF";
    public double BorderThickness { get; set; } = 1.0;
    public BorderStyle BorderStyle { get; set; } = BorderStyle.Solid;
    public bool ShowTitle { get; set; } = true;
    public double TitleFontSize { get; set; } = 12.0;
    public string TitleFontColor { get; set; } = "#FFFFFFFF";
    public double CornerRadius { get; set; } = 4.0;
}

public class IconOrderEntry
{
    public string IconPath { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

public class Container
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "새 Container";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 220.0;
    public double Height { get; set; } = 160.0;
    public SortMode SortMode { get; set; } = SortMode.NameAsc;
    public ContainerStyle Style { get; set; } = new();
    public List<Guid> LinkedRuleIds { get; set; } = new();
    public List<IconOrderEntry> IconOrder { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
