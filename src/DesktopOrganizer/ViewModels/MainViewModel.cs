using System.Collections.ObjectModel;
using System.Windows;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using DesktopOrganizer.ViewModels.Base;
using DesktopOrganizer.Views.Dialogs;
// UseWindowsForms=true: resolve MessageBox ambiguity
using MessageBox = System.Windows.MessageBox;

namespace DesktopOrganizer.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly ContainerService _containerService;
    private readonly RuleService      _ruleService;
    private readonly SettingsService  _settings;
    private LayoutService?            _layoutService;
    private bool _watcherEnabled = true;

    public MainViewModel(ContainerService containerService,
                         RuleService      ruleService,
                         SettingsService  settings)
    {
        _containerService = containerService;
        _ruleService      = ruleService;
        _settings         = settings;
        LoadContainers();
    }

    /// <summary>Injected after construction to avoid circular dependency.</summary>
    public void SetLayoutService(LayoutService layoutService)
        => _layoutService = layoutService;

    public ObservableCollection<ContainerViewModel> Containers { get; } = new();

    public bool WatcherEnabled
    {
        get => _watcherEnabled;
        set => SetField(ref _watcherEnabled, value);
    }

    // ── F-004 ────────────────────────────────────────────────────

    /// <summary>Creates a container at the given overlay coordinates and immediately enters rename mode.</summary>
    public void CreateContainerAt(double x, double y)
    {
        var model = _containerService.Create(x, y);
        var vm    = WrapContainer(model);
        Containers.Add(vm);
        vm.BeginRename();
    }

    // ── Startup load ─────────────────────────────────────────────

    private void LoadContainers()
    {
        foreach (var c in _containerService.GetAll())
            Containers.Add(WrapContainer(c));
    }

    private ContainerViewModel WrapContainer(Container model)
    {
        var vm = new ContainerViewModel(model, _containerService);
        vm.DeleteRequested += OnDeleteRequested;
        return vm;
    }

    // ── F-012 ~ F-015: Rule manager ──────────────────────────────

    /// <summary>Opens the Rule management dialog (F-012~F-015).</summary>
    public void OpenRuleManager(Window owner)
    {
        var dialog = new RuleManagerDialog(_ruleService, _settings) { Owner = owner };
        dialog.ShowDialog();
    }

    // ── F-020: Save layout ───────────────────────────────────────

    public void SaveLayout(Window owner)
    {
        if (_layoutService is null) return;

        var nameDialog = new LayoutNameDialog { Owner = owner };
        if (nameDialog.ShowDialog() != true || nameDialog.ResultName is null) return;

        var name = nameDialog.ResultName;

        // Check for duplicate name
        if (_layoutService.GetAll().Any(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            var overwrite = MessageBox.Show(
                $"'{name}' 이름의 Layout이 이미 존재합니다. 덮어쓰시겠습니까?",
                "이름 중복",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (overwrite != MessageBoxResult.Yes) return;

            var existing = _layoutService.GetAll()
                .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) _layoutService.Delete(existing.Id);
        }

        var layout = _layoutService.Capture(name, _settings);
        _layoutService.Save(layout);

        MessageBox.Show($"Layout '{name}'이(가) 저장됐습니다.", "저장 완료",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── F-021: Open layout manager ───────────────────────────────

    public void OpenLayoutManager(Window owner)
    {
        if (_layoutService is null) return;
        var dialog = new LayoutManagerDialog(_layoutService, this) { Owner = owner };
        dialog.ShowDialog();
    }

    /// <summary>
    /// Restores containers from <paramref name="layout"/> and rebuilds the overlay.
    /// Auto-saves current state as "_auto_before_restore" before applying.
    /// Returns list of missing icon paths.
    /// </summary>
    public IReadOnlyList<string> RestoreLayout(Layout layout)
    {
        if (_layoutService is null) return Array.Empty<string>();

        // Auto-save current state
        var autoSave = _layoutService.Capture("_auto_before_restore", _settings);
        _layoutService.Save(autoSave);

        // Restore to settings
        var missing = _layoutService.Restore(layout, _settings,
            currentDesktopIcons: Array.Empty<IconInfo>());

        // Rebuild the ViewModel collection from the updated settings
        foreach (var vm in Containers) vm.DeleteRequested -= OnDeleteRequested;
        Containers.Clear();
        LoadContainers();

        return missing;
    }

    // ── F-006 ────────────────────────────────────────────────────

    private void OnDeleteRequested(object? sender, EventArgs e)
    {
        if (sender is not ContainerViewModel vm) return;

        var result = MessageBox.Show(
            $"'{vm.Name}' Container를 삭제하시겠습니까?\n\nContainer 내 아이콘은 삭제되지 않습니다.",
            "Container 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.OK) return;

        _containerService.Delete(vm.Id);
        Containers.Remove(vm);
        vm.DeleteRequested -= OnDeleteRequested;
    }
}
