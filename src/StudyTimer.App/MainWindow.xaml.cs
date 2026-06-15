using System.ComponentModel;
using System.Windows;
using StudyTimer.App.ViewModels;
using StudyTimer.Core.Models;

namespace StudyTimer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private CompactWindow? _compactWindow;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.ErrorOccurred += ShowError;
        viewModel.CompactRequested += ShowCompactWindow;
    }

    private void ShowCompactWindow()
    {
        _compactWindow ??= new CompactWindow(_viewModel);
        _compactWindow.ExpandRequested -= RestoreMainWindow;
        _compactWindow.ExpandRequested += RestoreMainWindow;
        Hide();
        _compactWindow.Show();
        _compactWindow.Activate();
    }

    private void RestoreMainWindow()
    {
        _compactWindow?.Hide();
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void AddSession_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SessionEditDialog(DateOnly.FromDateTime(_viewModel.HistoryDate)) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultSession is not null)
        {
            _viewModel.AddHistorySession(dialog.ResultSession);
        }
    }

    private void EditSession_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not SessionRow row)
        {
            MessageBox.Show("请先选择一条学习明细。", "修改记录", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SessionEditDialog(DateOnly.FromDateTime(_viewModel.HistoryDate), row.Session) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultSession is not null)
        {
            _viewModel.UpdateHistorySession(row.Session, dialog.ResultSession);
        }
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryGrid.SelectedItem is not SessionRow row)
        {
            MessageBox.Show("请先选择一条学习明细。", "删除记录", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show("确定删除这条学习记录吗？", "删除记录", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _viewModel.DeleteHistorySession(row.Session);
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        if (_viewModel.Status != TimerStatus.Stopped)
        {
            var result = MessageBox.Show(
                "计时尚未结束。\n\n是：停止并保存后退出\n否：保留恢复状态并退出\n取消：继续使用",
                "退出学习计时器", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
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
    }

    private void ShowError(string message) => Dispatcher.Invoke(() =>
        MessageBox.Show(message, "学习计时器", MessageBoxButton.OK, MessageBoxImage.Warning));
}
