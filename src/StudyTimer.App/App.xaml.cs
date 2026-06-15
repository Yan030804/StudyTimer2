using System.Threading;
using System.Windows;
using StudyTimer.App.Services;
using StudyTimer.App.ViewModels;
using StudyTimer.Core.Models;
using StudyTimer.Core.Services;
using StudyTimer.Core.Storage;

namespace StudyTimer.App;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private TrayIconController? _trayIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "StudyTimer.Desktop.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("学习计时器已经在运行，请从任务栏或系统托盘打开。", "学习计时器",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var repository = new StudyRecordRepository();
        var recoveryStore = new TimerRecoveryStore(repository.RootPath);
        var settingsStore = new AppSettingsStore(repository.RootPath);
        var subjectService = new SubjectService(settingsStore, repository);
        var viewModel = new MainViewModel(repository, recoveryStore, subjectService);

        RestoreIfAvailable(viewModel, recoveryStore);

        _mainWindow = new MainWindow(viewModel);
        MainWindow = _mainWindow;
        _trayIcon = new TrayIconController(_mainWindow, viewModel, RequestExit);
        _mainWindow.HiddenToTray += () => _trayIcon.ShowHiddenTip();
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        if (_singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was not acquired by this instance.
            }
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }

    private void RequestExit()
    {
        if (_mainWindow?.TryExitApplication() != true)
        {
            return;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
        Shutdown();
    }

    private static void RestoreIfAvailable(MainViewModel viewModel, TimerRecoveryStore recoveryStore)
    {
        try
        {
            var snapshot = recoveryStore.Load();
            if (snapshot is null || snapshot.Status == TimerStatus.Stopped)
            {
                return;
            }

            var answer = MessageBox.Show(
                $"检测到上次未正常结束的计时记录。\n最后可靠时间：{snapshot.LastHeartbeat:yyyy-MM-dd HH:mm:ss}\n\n是否恢复这段学习时间？",
                "恢复学习计时", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                viewModel.Restore(snapshot);
            }
            else
            {
                recoveryStore.Clear();
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show($"恢复文件无法读取，将忽略本次恢复。\n{exception.Message}", "恢复提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            recoveryStore.Clear();
        }
    }
}
