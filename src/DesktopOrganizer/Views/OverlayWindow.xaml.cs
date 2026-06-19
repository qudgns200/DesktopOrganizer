using System.Diagnostics;
using System.Text;
using System.Windows;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using DesktopOrganizer.ViewModels;

namespace DesktopOrganizer.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Phase 1 verification: read desktop icons and display summary
        var settings = new AppSettings();
        var reader = new DesktopReaderService();
        var classifier = new FileClassifierService();
        var exclusion = new ExclusionService(settings);

        var icons = reader.ReadDesktopIcons();
        classifier.ClassifyAll(icons);
        exclusion.ApplyExclusion(icons);

        SummaryText.Text = BuildSummary(icons);
        Debug.WriteLine($"[Phase 1] Desktop scan complete — {icons.Count} icons");
    }

    private static string BuildSummary(List<IconInfo> icons)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Desktop Organizer — Phase 1 확인창");
        sb.AppendLine($"아이콘 총 {icons.Count}개 감지됨");
        sb.AppendLine(new string('─', 48));

        var groups = icons
            .GroupBy(i => i.Category)
            .OrderByDescending(g => g.Count());

        foreach (var g in groups)
            sb.AppendLine($"  {g.Key,-15} {g.Count(),3}개");

        sb.AppendLine(new string('─', 48));

        int sysCount = icons.Count(i => i.IsSystemIcon);
        sb.AppendLine($"  시스템 아이콘 (제외 대상): {sysCount}개");

        sb.AppendLine();
        sb.AppendLine("[ 아이콘 목록 ]");
        foreach (var icon in icons.OrderBy(i => i.FileName))
        {
            string flag = icon.IsSystemIcon ? "[SYS]" : "     ";
            sb.AppendLine($"  {flag} ({icon.X,5},{icon.Y,5})  {icon.FileName}");
        }

        return sb.ToString();
    }
}
