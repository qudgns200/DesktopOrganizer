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
    // Frosted-card defaults (F-009). The BODY fill is kept intentionally translucent so the
    // real desktop icons underneath stay clearly visible (비침습 원칙); visual weight lives in
    // the header bar, the soft border, the drop shadow, and the accent colour.
    public string BackgroundColor { get; set; } = "#59171A21";  // cool dark slate, ~35% alpha
    public double BackgroundOpacity { get; set; } = 0.5;         // effective body alpha ≈ 0.18
    public string BorderColor { get; set; } = "#40FFFFFF";       // subtle 25% white hairline
    public double BorderThickness { get; set; } = 1.0;
    public BorderStyle BorderStyle { get; set; } = BorderStyle.Solid;
    public bool ShowTitle { get; set; } = true;
    public double TitleFontSize { get; set; } = 12.0;
    public string TitleFontColor { get; set; } = "#FFFFFFFF";
    public double CornerRadius { get; set; } = 10.0;             // rounded card

    /// <summary>Accent colour for the header dot/strip (F-009 / Phase 12 F-027 preview).</summary>
    public string AccentColor { get; set; } = "#FF4C8DFF";       // pleasant blue
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
