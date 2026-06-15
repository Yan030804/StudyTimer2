namespace StudyTimer.Core.Models;

public sealed record StudySession
{
    public StudySession(
        DateTime start,
        DateTime end,
        Guid? subjectId = null,
        string? subjectName = null)
    {
        if (end <= start)
        {
            throw new ArgumentException("End time must be later than start time.", nameof(end));
        }

        Start = start;
        End = end;
        SubjectId = subjectId ?? SubjectDefinition.UncategorizedId;
        SubjectName = string.IsNullOrWhiteSpace(subjectName)
            ? SubjectDefinition.UncategorizedName
            : subjectName.Trim();
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public Guid SubjectId { get; }

    public string SubjectName { get; }

    public TimeSpan Duration => End - Start;

    public StudySession WithSubject(SubjectDefinition subject) =>
        new(Start, End, subject.Id, subject.Name);
}
