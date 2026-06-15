using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using StudyTimer.Core.Models;
using StudyTimer.Core.Services;

namespace StudyTimer.App;

public partial class SubjectManagementDialog : Window
{
    private readonly SubjectService _subjectService;
    private readonly ObservableCollection<SubjectRow> _rows = [];

    public SubjectManagementDialog(SubjectService subjectService)
    {
        InitializeComponent();
        _subjectService = subjectService;
        SubjectGrid.ItemsSource = _rows;
        RefreshRows();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SubjectEditDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Run(() => _subjectService.Add(dialog.SubjectName, dialog.SubjectColor));
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SubjectGrid.SelectedItem is not SubjectRow row || row.Subject.IsBuiltIn)
        {
            return;
        }

        var dialog = new SubjectEditDialog(row.Subject) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            Run(() => _subjectService.Rename(row.Subject.Id, dialog.SubjectName, dialog.SubjectColor));
        }
    }

    private void Archive_Click(object sender, RoutedEventArgs e)
    {
        if (SubjectGrid.SelectedItem is not SubjectRow row || row.Subject.IsBuiltIn)
        {
            return;
        }

        Run(() => _subjectService.SetArchived(row.Subject.Id, !row.Subject.IsArchived));
    }

    private void SubjectGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = SubjectGrid.SelectedItem as SubjectRow;
        var editable = row is not null && !row.Subject.IsBuiltIn;
        EditButton.IsEnabled = editable;
        ArchiveButton.IsEnabled = editable;
        ArchiveButton.Content = row?.Subject.IsArchived == true ? "恢复" : "归档";
    }

    private void Run(Action action)
    {
        try
        {
            action();
            RefreshRows();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "科目管理", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshRows()
    {
        _rows.Clear();
        foreach (var subject in _subjectService.Subjects)
        {
            _rows.Add(new SubjectRow(subject));
        }
    }

    private sealed record SubjectRow(SubjectDefinition Subject)
    {
        public string Name => Subject.Name;
        public string Color => Subject.Color;
        public string StatusText => Subject.IsBuiltIn ? "内置" : Subject.IsArchived ? "已归档" : "使用中";
    }
}
