namespace StudyTimer.App.ViewModels;

public sealed record SubjectFilterOption(Guid? SubjectId, string Name)
{
    public static SubjectFilterOption All { get; } = new(null, "全部科目");
}
