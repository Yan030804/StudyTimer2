using System.Windows;
using StudyTimer.Core.Models;
using StudyTimer.Core.Services;

namespace StudyTimer.App;

public partial class SubjectEditDialog : Window
{
    public SubjectEditDialog(SubjectDefinition? subject = null)
    {
        InitializeComponent();
        ColorComboBox.ItemsSource = SubjectService.ColorPalette;
        NameTextBox.Text = subject?.Name ?? string.Empty;
        ColorComboBox.SelectedItem = subject?.Color ?? SubjectService.ColorPalette[0];
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public string SubjectName => NameTextBox.Text;
    public string SubjectColor => ColorComboBox.SelectedItem as string ?? SubjectService.ColorPalette[0];

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("请输入科目名称。", "科目", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
