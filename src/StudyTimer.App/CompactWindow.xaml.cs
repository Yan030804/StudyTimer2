using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using StudyTimer.App.ViewModels;

namespace StudyTimer.App;

public partial class CompactWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;
    private bool _restoringPlacement;
    private bool _displayEventsSubscribed;

    public CompactWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public event Action? ExpandRequested;

    public void ShowCompact()
    {
        RestorePlacement();
        Show();
        Activate();
    }

    public void CloseForApplicationExit()
    {
        _allowClose = true;
        Close();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestorePlacement();
        ClampToVisibleWorkArea();
        if (!_displayEventsSubscribed)
        {
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            _displayEventsSubscribed = true;
        }
    }

    private void RestorePlacement()
    {
        _restoringPlacement = true;
        Topmost = _viewModel.SubjectService.Settings.CompactTopmost;
        UpdateTopmostButton();

        var left = _viewModel.SubjectService.Settings.CompactLeft
            ?? SystemParameters.WorkArea.Right - Width - 24;
        var top = _viewModel.SubjectService.Settings.CompactTop
            ?? SystemParameters.WorkArea.Top + 24;
        Left = Math.Clamp(left, SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width);
        Top = Math.Clamp(top, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height);
        _restoringPlacement = false;
    }

    private void ClampToVisibleWorkArea()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        var topLeft = transform.Transform(new Point(info.WorkArea.Left, info.WorkArea.Top));
        var bottomRight = transform.Transform(new Point(info.WorkArea.Right, info.WorkArea.Bottom));
        var maxLeft = Math.Max(topLeft.X, bottomRight.X - ActualWidth);
        var maxTop = Math.Max(topLeft.Y, bottomRight.Y - ActualHeight);

        _restoringPlacement = true;
        Left = Math.Clamp(Left, topLeft.X, maxLeft);
        Top = Math.Clamp(Top, topLeft.Y, maxTop);
        _restoringPlacement = false;
        _viewModel.SubjectService.UpdateCompactWindow(Left, Top, Topmost);
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(ClampToVisibleWorkArea);

    private void Topmost_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _viewModel.SubjectService.UpdateCompactTopmost(Topmost);
        UpdateTopmostButton();
    }

    private void UpdateTopmostButton()
    {
        TopmostButton.Content = Topmost ? "取消置顶" : "置顶";
        TopmostButton.MinWidth = Topmost ? 82 : 58;
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (!_restoringPlacement && IsLoaded && double.IsFinite(Left) && double.IsFinite(Top))
        {
            _viewModel.SubjectService.UpdateCompactWindow(Left, Top, Topmost);
        }
    }

    private void Expand_Click(object sender, RoutedEventArgs e) => ExpandRequested?.Invoke();

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !HasButtonAncestor(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
    }

    private static bool HasButtonAncestor(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button)
            {
                return true;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        ExpandRequested?.Invoke();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (_displayEventsSubscribed)
        {
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            _displayEventsSubscribed = false;
        }
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
