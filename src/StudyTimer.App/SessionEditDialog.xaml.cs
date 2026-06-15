using System.Globalization;
using System.Windows;
using StudyTimer.Core.Models;

namespace StudyTimer.App;

public partial class SessionEditDialog : Window
{
    private static readonly string[] TimeFormats = ["h\\:mm", "hh\\:mm", "h\\:mm\\:ss", "hh\\:mm\\:ss"];
    private readonly DateOnly _date;

    public SessionEditDialog(DateOnly date, StudySession? session = null)
    {
        InitializeComponent();
        _date = date;
        DateText.Text = date.ToString("yyyy年M月d日", CultureInfo.CurrentCulture);
        StartTextBox.Text = session?.Start.ToString("HH:mm:ss") ?? "08:00:00";
        EndTextBox.Text = session is not null && session.End.Date > session.Start.Date
            ? "24:00:00"
            : session?.End.ToString("HH:mm:ss") ?? "09:00:00";
    }

    public StudySession? ResultSession { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseTime(StartTextBox.Text, allowMidnight24: false, out var startTime) ||
            !TryParseTime(EndTextBox.Text, allowMidnight24: true, out var endTime))
        {
            MessageBox.Show("请输入有效时间，例如 08:30:00。结束时间可以是 24:00:00。", "时间格式错误",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var start = _date.ToDateTime(TimeOnly.MinValue).Add(startTime);
        var end = endTime == TimeSpan.FromDays(1)
            ? _date.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : _date.ToDateTime(TimeOnly.MinValue).Add(endTime);

        if (end <= start)
        {
            MessageBox.Show("结束时间必须晚于开始时间。", "时间范围错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultSession = new StudySession(start, end);
        DialogResult = true;
    }

    private static bool TryParseTime(string text, bool allowMidnight24, out TimeSpan value)
    {
        if (allowMidnight24 && text.Trim() == "24:00:00")
        {
            value = TimeSpan.FromDays(1);
            return true;
        }

        return TimeSpan.TryParseExact(text.Trim(), TimeFormats, CultureInfo.InvariantCulture, out value) &&
               value >= TimeSpan.Zero && value < TimeSpan.FromDays(1);
    }
}
