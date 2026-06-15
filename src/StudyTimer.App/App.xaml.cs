using System.Windows;
using StudyTimer.App.ViewModels;
using StudyTimer.Core.Models;
using StudyTimer.Core.Storage;

namespace StudyTimer.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var repository = new StudyRecordRepository();
        var recoveryStore = new TimerRecoveryStore(repository.RootPath);
        var viewModel = new MainViewModel(repository, recoveryStore);

        try
        {
            var snapshot = recoveryStore.Load();
            if (snapshot is not null && snapshot.Status != TimerStatus.Stopped)
            {
                var answer = MessageBox.Show(
                    $"检测到上次未正常结束的计时记录。\n最后可靠时间：{snapshot.LastHeartbeat:yyyy-MM-dd HH:mm:ss}\n\n是否恢复这段学习时间？",
                    "恢复学习计时",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer == MessageBoxResult.Yes)
                {
                    viewModel.Restore(snapshot);
                }
                else
                {
                    recoveryStore.Clear();
                }
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show($"恢复文件无法读取，将忽略本次恢复。\n{exception.Message}", "恢复提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            recoveryStore.Clear();
        }

        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }
}
