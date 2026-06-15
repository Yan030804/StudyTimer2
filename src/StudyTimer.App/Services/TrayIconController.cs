using System.ComponentModel;
using System.Drawing;
using Forms = System.Windows.Forms;
using StudyTimer.App.ViewModels;
using StudyTimer.Core.Models;

namespace StudyTimer.App.Services;

public sealed class TrayIconController : IDisposable
{
    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;
    private readonly Action _exitAction;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private readonly Forms.ToolStripMenuItem _stopItem;

    public TrayIconController(MainWindow window, MainViewModel viewModel, Action exitAction)
    {
        _window = window;
        _viewModel = viewModel;
        _exitAction = exitAction;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => Dispatch(_window.ShowFromTray));
        menu.Items.Add("紧凑模式", null, (_, _) => Dispatch(_window.ShowCompactWindow));
        menu.Items.Add(new Forms.ToolStripSeparator());
        _toggleItem = new Forms.ToolStripMenuItem("开始学习", null,
            (_, _) => Dispatch(() => _viewModel.ToggleTimerCommand.Execute(null)));
        _stopItem = new Forms.ToolStripMenuItem("停止并保存", null,
            (_, _) => Dispatch(_viewModel.StopAndSave));
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_stopItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatch(_exitAction));

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "学习计时器",
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty),
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatch(_window.ShowFromTray);
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateMenu();
    }

    public void ShowHiddenTip()
    {
        _notifyIcon.BalloonTipTitle = "学习计时器仍在运行";
        _notifyIcon.BalloonTipText = "窗口已隐藏到系统托盘，双击托盘图标可恢复。";
        _notifyIcon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.ToggleText) or nameof(MainViewModel.Status))
        {
            Dispatch(UpdateMenu);
        }
    }

    private void UpdateMenu()
    {
        _toggleItem.Text = _viewModel.ToggleText;
        _stopItem.Enabled = _viewModel.Status != TimerStatus.Stopped;
    }

    private static void Dispatch(Action action) =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
}
