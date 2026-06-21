using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using DesktopOrganizer.Services;
using DesktopOrganizer.ViewModels;
// UseWindowsForms=true causes ambiguity: resolve both conflicting types
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DesktopOrganizer;

public partial class App : Application
{
    private const string MutexName = "DesktopOrganizer_SingleInstance_{A1B2C3D4}";
    private Mutex? _mutex;
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _pauseMenuItem;
    private bool _watcherPaused;
    private SettingsService? _settingsService;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Desktop Organizer가 이미 실행 중입니다.", "Desktop Organizer",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _settingsService = new SettingsService();
        _settingsService.Load();

        var containerService = new ContainerService(_settingsService);
        var mainVm           = new MainViewModel(containerService);

        InitializeTrayIcon();

        var overlay = new Views.OverlayWindow(mainVm);
        overlay.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    // ── Tray icon ────────────────────────────────────────────────

    private void InitializeTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Desktop Organizer",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        // 설정 열기 — dialog implemented in Phase 3+
        var settingsItem = new ToolStripMenuItem("설정 열기");
        settingsItem.Click += (_, _) => OnOpenSettingsClick();
        menu.Items.Add(settingsItem);

        // 감시 일시정지 / 재개 — watcher implemented in Phase 7
        _pauseMenuItem = new ToolStripMenuItem("감시 일시정지");
        _pauseMenuItem.Click += (_, _) => OnToggleWatcherClick();
        menu.Items.Add(_pauseMenuItem);

        menu.Items.Add(new ToolStripSeparator());

        // 종료
        var exitItem = new ToolStripMenuItem("종료");
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnOpenSettingsClick()
    {
        // Placeholder — settings dialog will be implemented in Phase 3
        MessageBox.Show("설정 기능은 Phase 3에서 구현됩니다.", "Desktop Organizer",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnToggleWatcherClick()
    {
        _watcherPaused = !_watcherPaused;
        if (_pauseMenuItem is not null)
            _pauseMenuItem.Text = _watcherPaused ? "감시 재개" : "감시 일시정지";

        // Actual watcher start/stop will be wired in Phase 7
    }
}
