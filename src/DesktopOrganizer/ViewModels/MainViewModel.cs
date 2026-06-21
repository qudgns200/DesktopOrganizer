using System.Collections.ObjectModel;
using System.Windows;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using DesktopOrganizer.ViewModels.Base;
// UseWindowsForms=true: resolve MessageBox ambiguity
using MessageBox = System.Windows.MessageBox;

namespace DesktopOrganizer.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly ContainerService _containerService;
    private bool _watcherEnabled = true;

    public MainViewModel(ContainerService containerService)
    {
        _containerService = containerService;
        LoadContainers();
    }

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
