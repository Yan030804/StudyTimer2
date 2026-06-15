using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StudyTimer.App.ViewModels;

namespace StudyTimer.App;

public partial class CompactWindow : Window
{
    private bool _allowClose;

    public CompactWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public event Action? ExpandRequested;

    public void CloseForApplicationExit()
    {
        _allowClose = true;
        Close();
    }

    private void Expand_Click(object sender, RoutedEventArgs e) => ExpandRequested?.Invoke();

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed &&
            !HasButtonAncestor(e.OriginalSource as DependencyObject))
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
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            ExpandRequested?.Invoke();
        }
    }
}
