using System.Globalization;
using StudyTimer.Core.Models;
using StudyTimer.Core.Storage;

namespace StudyTimer.Core.Services;

public sealed class StatisticsService(StudyRecordRepository repository)
{
    public StatisticsReport GetReport(
        StatisticsPeriod period,
        DateOnly anchorDate,
        DateOnly today,
        Guid? subjectId = null)
    {
        var (plotStart, plotEnd, title) = GetPlotRange(period, anchorDate);
        var effectiveEnd = plotEnd > today ? today : plotEnd;
        var effectiveStart = plotStart > effectiveEnd ? effectiveEnd : plotStart;
        var points = period switch
        {
            StatisticsPeriod.SevenDays => GetDailyPoints(plotStart, plotEnd, anchorDate, today, subjectId),
            StatisticsPeriod.Month => GetWeeklyPoints(plotStart, plotEnd, subjectId),
            StatisticsPeriod.Year => GetMonthlyPoints(plotStart.Year, subjectId),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };

        return new StatisticsReport(
            period,
            anchorDate,
            plotStart,
            plotEnd,
            effectiveStart,
            effectiveEnd,
            title,
            points,
            BuildSummary(effectiveStart, effectiveEnd, subjectId));
    }

    public IReadOnlyList<ChartPoint> GetLastSevenDays(DateOnly today) =>
        GetReport(StatisticsPeriod.SevenDays, today, today).Points;

    public IReadOnlyList<ChartPoint> GetWeeksOfMonth(int year, int month) =>
        GetReport(StatisticsPeriod.Month, new DateOnly(year, month, 1), DateOnly.MaxValue).Points;

    public IReadOnlyList<ChartPoint> GetMonthsOfYear(int year) =>
        GetReport(StatisticsPeriod.Year, new DateOnly(year, 1, 1), DateOnly.MaxValue).Points;

    public static DateOnly MoveAnchor(StatisticsPeriod period, DateOnly anchorDate, int direction) => period switch
    {
        StatisticsPeriod.SevenDays => anchorDate.AddDays(7 * direction),
        StatisticsPeriod.Month => anchorDate.AddMonths(direction),
        StatisticsPeriod.Year => anchorDate.AddYears(direction),
        _ => anchorDate
    };

    public static (DateOnly Start, DateOnly End, string Title) GetPlotRange(
        StatisticsPeriod period,
        DateOnly anchorDate) => period switch
    {
        StatisticsPeriod.SevenDays => (
            anchorDate.AddDays(-6),
            anchorDate,
            $"{anchorDate.AddDays(-6):yyyy年M月d日} - {anchorDate:yyyy年M月d日}"),
        StatisticsPeriod.Month => (
            new DateOnly(anchorDate.Year, anchorDate.Month, 1),
            new DateOnly(anchorDate.Year, anchorDate.Month, 1).AddMonths(1).AddDays(-1),
            $"{anchorDate:yyyy年M月}"),
        StatisticsPeriod.Year => (
            new DateOnly(anchorDate.Year, 1, 1),
            new DateOnly(anchorDate.Year, 12, 31),
            $"{anchorDate.Year}年"),
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    private IReadOnlyList<ChartPoint> GetDailyPoints(
        DateOnly start,
        DateOnly end,
        DateOnly anchorDate,
        DateOnly today,
        Guid? subjectId)
    {
        var points = new List<ChartPoint>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var label = date == today ? "今天" : date.ToString("M/d", CultureInfo.InvariantCulture);
            points.Add(new ChartPoint(label, repository.GetDuration(date, subjectId)));
        }

        return points;
    }

    private IReadOnlyList<ChartPoint> GetWeeklyPoints(DateOnly monthStart, DateOnly monthEnd, Guid? subjectId)
    {
        var cursor = StartOfWeek(monthStart);
        var points = new List<ChartPoint>();
        var weekNumber = 1;
        while (cursor <= monthEnd)
        {
            var weekEnd = cursor.AddDays(6);
            var rangeStart = cursor < monthStart ? monthStart : cursor;
            var rangeEnd = weekEnd > monthEnd ? monthEnd : weekEnd;
            points.Add(new ChartPoint(
                $"第{weekNumber}周\n{rangeStart:M/d}-{rangeEnd:M/d}",
                SumRange(rangeStart, rangeEnd, subjectId)));
            cursor = cursor.AddDays(7);
            weekNumber++;
        }

        return points;
    }

    private IReadOnlyList<ChartPoint> GetMonthlyPoints(int year, Guid? subjectId) =>
        Enumerable.Range(1, 12)
            .Select(month =>
            {
                var start = new DateOnly(year, month, 1);
                return new ChartPoint($"{month}月", SumRange(start, start.AddMonths(1).AddDays(-1), subjectId));
            })
            .ToArray();

    private StatisticsSummary BuildSummary(DateOnly start, DateOnly end, Guid? subjectId)
    {
        if (end < start)
        {
            return new StatisticsSummary(TimeSpan.Zero, TimeSpan.Zero, null, TimeSpan.Zero, 0);
        }

        var daily = new List<(DateOnly Date, TimeSpan Duration)>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            daily.Add((date, repository.GetDuration(date, subjectId)));
        }

        var totalTicks = daily.Sum(item => item.Duration.Ticks);
        var total = TimeSpan.FromTicks(totalTicks);
        var average = TimeSpan.FromTicks(totalTicks / Math.Max(1, daily.Count));
        var longest = daily
            .Where(item => item.Duration > TimeSpan.Zero)
            .OrderByDescending(item => item.Duration)
            .ThenBy(item => item.Date)
            .FirstOrDefault();

        var longestStreak = 0;
        var currentStreak = 0;
        foreach (var item in daily)
        {
            if (item.Duration > TimeSpan.Zero)
            {
                currentStreak++;
                longestStreak = Math.Max(longestStreak, currentStreak);
            }
            else
            {
                currentStreak = 0;
            }
        }

        return new StatisticsSummary(
            total,
            average,
            longest.Date == default ? null : longest.Date,
            longest.Duration,
            longestStreak);
    }

    private TimeSpan SumRange(DateOnly start, DateOnly end, Guid? subjectId)
    {
        long ticks = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            ticks += repository.GetDuration(date, subjectId).Ticks;
        }

        return TimeSpan.FromTicks(ticks);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
