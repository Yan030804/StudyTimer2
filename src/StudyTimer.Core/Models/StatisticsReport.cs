namespace StudyTimer.Core.Models;

public enum StatisticsPeriod
{
    SevenDays,
    Month,
    Year
}

public sealed record StatisticsSummary(
    TimeSpan TotalDuration,
    TimeSpan AveragePerCalendarDay,
    DateOnly? LongestDay,
    TimeSpan LongestDayDuration,
    int LongestStreakDays);

public sealed record StatisticsReport(
    StatisticsPeriod Period,
    DateOnly AnchorDate,
    DateOnly PlotStart,
    DateOnly PlotEnd,
    DateOnly EffectiveStart,
    DateOnly EffectiveEnd,
    string Title,
    IReadOnlyList<ChartPoint> Points,
    IReadOnlyList<SubjectSharePoint> SubjectShares,
    StatisticsSummary Summary);
