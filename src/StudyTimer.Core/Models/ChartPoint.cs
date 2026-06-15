namespace StudyTimer.Core.Models;

public sealed record ChartPoint(string Label, TimeSpan Duration)
{
    public double Hours => Duration.TotalHours;
}
