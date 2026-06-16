namespace StudyTimer.Core.Models;

public sealed record SubjectSharePoint(
    Guid SubjectId,
    string SubjectName,
    string Color,
    TimeSpan Duration,
    double Percentage);
