namespace StudyTimer.Core.Models;

public sealed record StudySession
{
    public StudySession(DateTime start, DateTime end)
    {
        if (end <= start)
        {
            throw new ArgumentException("End time must be later than start time.", nameof(end));
        }

        Start = start;
        End = end;
    }

    public DateTime Start { get; }

    public DateTime End { get; }

    public TimeSpan Duration => End - Start;
}
