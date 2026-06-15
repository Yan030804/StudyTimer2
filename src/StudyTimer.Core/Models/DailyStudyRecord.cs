namespace StudyTimer.Core.Models;

public sealed record DailyStudyRecord(DateOnly Date, IReadOnlyList<StudySession> Sessions)
{
    public TimeSpan TotalDuration => TimeSpan.FromTicks(Sessions.Sum(session => session.Duration.Ticks));

    public static DailyStudyRecord Empty(DateOnly date) => new(date, Array.Empty<StudySession>());
}
