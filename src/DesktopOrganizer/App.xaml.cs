using System.Diagnostics;
using System.Drawing;
using System.IO;
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
    private SettingsService?       _settingsService;
    private LayoutService?         _layoutService;
    private DesktopWatcherService? _watcherService;
    private AutoOrganizeService?   _autoOrganize;
    private MainViewModel?         _mainVm;

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

        // F-022: apply saved log level before any other service logs
        LogService.Instance.MinLevel = _settingsService.Config.Settings.LogLevel.ToLogLevel();
        LogService.Instance.Info("App", "Desktop Organizer starting");

        var containerService = new ContainerService(_settingsService);
        var ruleService      = new RuleService(_settingsService);
        _layoutService       = new LayoutService(_settingsService);
        _mainVm              = new MainViewModel(containerService, ruleService, _settingsService);
        _mainVm.SetLayoutService(_layoutService);

        // F-016 / F-017: desktop watcher + auto-organizer
        _watcherService = new DesktopWatcherService();
        _autoOrganize   = new AutoOrganizeService(
            new DesktopReaderService(),
            new ExclusionService(_settingsService.Config.Settings),
            new FileClassifierService(),
            ruleService,
            _settingsService,
            _watcherService,
            new IconSortService(),
            new IconOrderService(_settingsService));
        _autoOrganize.Initialize();
        _mainVm.SetAutoOrganize(_autoOrganize);

        InitializeTrayIcon();

        var overlay = new Views.OverlayWindow(_mainVm);
        overlay.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.Instance.Info("App", "Desktop Organizer shutting down");
        _autoOrganize?.Dispose();
        _watcherService?.Dispose();
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

        // Container / Rule / Layout 관리
        var newContainerItem = new ToolStripMenuItem("새 Container");
        newContainerItem.Click += (_, _) => Dispatcher.Invoke(() =>
            _mainVm?.CreateContainerAt(100, 100));
        menu.Items.Add(newContainerItem);

        menu.Items.Add(new ToolStripSeparator());

        var ruleItem = new ToolStripMenuItem("Rule 관리...");
        ruleItem.Click += (_, _) => Dispatcher.Invoke(() =>
            _mainVm?.OpenRuleManager());
        menu.Items.Add(ruleItem);

        var saveLayoutItem = new ToolStripMenuItem("Layout 저장...");
        saveLayoutItem.Click += (_, _) => Dispatcher.Invoke(() =>
            _mainVm?.SaveLayout());
        menu.Items.Add(saveLayoutItem);

        var manageLayoutItem = new ToolStripMenuItem("Layout 관리...");
        manageLayoutItem.Click += (_, _) => Dispatcher.Invoke(() =>
            _mainVm?.OpenLayoutManager());
        menu.Items.Add(manageLayoutItem);

        menu.Items.Add(new ToolStripSeparator());

        // 감시 일시정지 / 재개
        _pauseMenuItem = new ToolStripMenuItem("감시 일시정지");
        _pauseMenuItem.Click += (_, _) => OnToggleWatcherClick();
        menu.Items.Add(_pauseMenuItem);

        menu.Items.Add(new ToolStripSeparator());

        // 로그 파일 열기
        var openLogItem = new ToolStripMenuItem("로그 파일 열기");
        openLogItem.Click += (_, _) => OnOpenLogFileClick();
        menu.Items.Add(openLogItem);

        menu.Items.Add(new ToolStripSeparator());

        // 종료
        var exitItem = new ToolStripMenuItem("종료");
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnOpenLogFileClick()
    {
        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopOrganizer", "logs");

        if (!Directory.Exists(logsDir))
        {
            MessageBox.Show("아직 기록된 로그 파일이 없습니다.", "Desktop Organizer",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Open today's log file if it exists; otherwise open the logs directory
        var todayLog = Path.Combine(logsDir,
            $"desktop_organizer_{DateTime.Now:yyyyMMdd}.log");

        var target = File.Exists(todayLog) ? todayLog : logsDir;
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private void OnToggleWatcherClick()
    {
        _watcherPaused = !_watcherPaused;
        if (_pauseMenuItem is not null)
            _pauseMenuItem.Text = _watcherPaused ? "감시 재개" : "감시 일시정지";

        if (_watcherPaused) _watcherService?.Stop();
        else                _watcherService?.Start();
    }
}
