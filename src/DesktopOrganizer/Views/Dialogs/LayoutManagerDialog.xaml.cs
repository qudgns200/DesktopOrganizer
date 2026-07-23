using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using DesktopOrganizer.ViewModels;
// UseWindowsForms=true
using Button      = System.Windows.Controls.Button;
using MessageBox  = System.Windows.MessageBox;

namespace DesktopOrganizer.Views.Dialogs;

public partial class LayoutManagerDialog : Window
{
    private readonly LayoutService  _layoutService;
    private readonly MainViewModel  _mainVm;

    public ObservableCollection<LayoutItemViewModel> Layouts { get; } = new();

    public LayoutManagerDialog(LayoutService layoutService, MainViewModel mainVm)
    {
        _layoutService = layoutService;
        _mainVm        = mainVm;
        InitializeComponent();
        DataContext = this;
        LoadLayouts();
    }

    // ── List helpers ──────────────────────────────────────────────

    private void LoadLayouts()
    {
        Layouts.Clear();
        foreach (var layout in _layoutService.GetAll())
            Layouts.Add(new LayoutItemViewModel(layout));
    }

    // ── F-021: Restore ────────────────────────────────────────────

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid id) return;

        var layout = _layoutService.Load(id);
        if (layout is null)
        {
            MessageBox.Show("Layout 파일을 읽을 수 없습니다.", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show(
            "현재 Layout이 선택한 Layout으로 교체됩니다.\n저장되지 않은 변경 사항이 있으면 먼저 저장하세요.\n\n계속하시겠습니까?",
            "Layout 복원",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var missing = _mainVm.RestoreLayout(layout);

        if (missing.Count > 0)
        {
            var list = string.Join("\n", missing.Take(5).Select(Path.GetFileName));
            var more = missing.Count > 5 ? $"\n…외 {missing.Count - 5}개" : string.Empty;
            MessageBox.Show(
                $"다음 아이콘은 현재 바탕화면에 없어 복원에서 제외됐습니다:\n\n{list}{more}",
                "일부 아이콘 누락",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        LoadLayouts();
    }

    // ── Delete ────────────────────────────────────────────────────

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Guid id) return;

        var item = Layouts.FirstOrDefault(l => l.Id == id);
        if (item is null) return;

        var result = MessageBox.Show(
            $"'{item.Name}' Layout을 삭제하시겠습니까?",
            "Layout 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.OK) return;

        _layoutService.Delete(id);
        LoadLayouts();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}

/// <summary>Display ViewModel for a single row in the layout list.</summary>
public class LayoutItemViewModel
{
    public Guid   Id   { get; }
    public string Name { get; }
    public string Meta { get; }

    public LayoutItemViewModel(Layout layout)
    {
        Id   = layout.Id;
        Name = layout.Name;
        Meta = $"{layout.SavedAt.ToLocalTime():yyyy-MM-dd HH:mm}  |  " +
               $"{layout.ScreenWidth}×{layout.ScreenHeight}  |  " +
               $"Container {layout.Containers.Count}개";
    }
}
