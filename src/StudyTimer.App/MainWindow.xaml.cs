using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using StudyTimer.App.ViewModels;
using StudyTimer.Core.Models;

namespace StudyTimer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private CompactWindow? _compactWindow;
    private bool _allowClose;
    private bool _hiddenTipShown;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.ErrorOccurred += ShowError;
        viewModel.CompactRequested += ShowCompactWindow;
        viewModel.ManageSubjectsRequested += ShowSubjectManager;
    }

    public event Action? HiddenToTray;

    public void ShowFromTray()
    {
        _compactWindow?.Hide();
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void ShowCompactWindow()
    {
        _compactWindow ??= new CompactWindow(_viewModel);
        _compactWindow.ExpandRequested -= ShowFromTray;
        _compactWindow.ExpandRequested += ShowFromTray;
        Hide();
        _compactWindow.ShowCompact();
    }

    public bool TryExitApplication()
    {
        if (_viewModel.Status != TimerStatus.Stopped)
        {
            var result = MessageBox.Show(
                "计时尚未结束。\n\n是：停止并保存后退出\n否：保留恢复状态并退出\n取消：继续使用",
                "退出学习计时器", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel)
            {
                return false;
            }

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.StopAndSave();
            }
            else
            {
                _viewModel.SaveRecovery();
            }
        }

        _allowClose = true;
        _compactWindow?.CloseForApplicationExit();
        Close();
        return true;
    }

    private void ShowSubjectManager()
    {
        var dialog = new SubjectManagementDialog(_viewModel.SubjectService) { Owner = this };
        dialog.ShowDialog();
        _viewModel.ReloadSubjects();
        _viewModel.RefreshAll();
    }

    private void StatisticsPeriod_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not RadioButton button || button.Tag is not string tag)
        {
            return;
        }

        viewModel.SetStatisticsPeriod(Enum.Parse<StatisticsPeriod>(tag));
    }

    private void StatisticsPrevious_Click(object sender, RoutedEventArgs e) => _viewModel.NavigateStatistics(-1);
    private void StatisticsNext_Click(object sender, RoutedEventArgs e) => _viewModel.NavigateStatistics(1);
    private void StatisticsToday_Click(object sender, RoutedEventArgs e) => _viewModel.GoToCurrentStatisticsPeriod();
    private void HistoryPrevious_Click(object sender, RoutedEventArgs e) => _viewModel.NavigateHistory(-1);
    private void HistoryNext_Click(object sender, RoutedEventArgs e) => _viewModel.NavigateHistory(1);
    private void HistoryToday_Click(object sender, RoutedEventArgs e) => _viewModel.GoToTodayHistory();

    private void ExportCurrent_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateCsvDialog($"学习统计-{DateTime.Now:yyyyMMdd}.csv");
        if (dialog.ShowDialog(this) == true)
        {
            RunExport(() => _viewModel.ExportCurrentRange(dialog.FileName), dialog.FileName);
        }
    }

    private void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateCsvDialog($"全部学习记录-{DateTime.Now:yyyyMMdd}.csv");
        if (dialog.ShowDialog(this) == true)
        {
            RunExport(() => _viewModel.ExportAll(dialog.FileName), dialog.FileName);
        }
    }

    private static SaveFileDialog CreateCsvDialog(string fileName) => new()
    {
        Title = "导出学习记录",
        Filter = "CSV 文件 (*.csv)|*.csv",
        FileName = fileName,
        AddExtension = true,
        DefaultExt = ".csv"
    };

    private void RunExport(Action export, string fileName)
    {
        try
        {
            export();
            MessageBox.Show($"数据已导出：\n{fileName}", "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowError($"导出失败：{exception.Message}");
        }
    }

    private void AddSession_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SessionEditDialog(
            DateOnly.FromDateTime(_viewModel.HistoryDate),
            _viewModel.SubjectService.ActiveSubjects) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultSession is not null)
        {
            _viewModel.AddHistorySession(dialog.ResultSession);
        }
    }

    private void EditSession_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not SessionRow row)
        {
            return;
        }

        var dialog = new SessionEditDialog(
            DateOnly.FromDateTime(_viewModel.HistoryDate),
            _viewModel.SubjectService.ActiveSubjects,
            row.Session) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultSession is not null)
        {
            _viewModel.UpdateHistorySession(row.Session, dialog.ResultSession);
        }
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not SessionRow row)
        {
            return;
        }

        if (MessageBox.Show("确定删除这条学习记录吗？", "删除记录", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _viewModel.DeleteHistorySession(row.Session);
        }
    }

    private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var enabled = HistoryGrid.SelectedItem is SessionRow;
        EditHistoryButton.IsEnabled = enabled;
        DeleteHistoryButton.IsEnabled = enabled;
    }

    private void HistoryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryGrid.SelectedItem is SessionRow)
        {
            EditSession_Click(sender, e);
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        _compactWindow?.Hide();
        if (!_hiddenTipShown)
        {
            _hiddenTipShown = true;
            HiddenToTray?.Invoke();
        }
    }

    private void ShowError(string message) => Dispatcher.Invoke(() =>
        MessageBox.Show(message, "学习计时器", MessageBoxButton.OK, MessageBoxImage.Warning));
}
