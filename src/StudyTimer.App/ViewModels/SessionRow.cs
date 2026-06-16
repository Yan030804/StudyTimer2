using StudyTimer.Core.Models;
using StudyTimer.Core.Services;

namespace StudyTimer.App.ViewModels;

public sealed record SessionRow(StudySession Session, string SubjectColor)
{
    public string SubjectName => Session.SubjectName;

    public string SubjectBackground => SubjectColorHelper.SoftBackground(SubjectColor);

    public string StartText => Session.Start.ToString("HH:mm:ss");

    public string EndText => Session.End.Date > Session.Start.Date ? "24:00:00" : Session.End.ToString("HH:mm:ss");

    public string DurationText => DurationFormatter.Clock(Session.Duration);
}
