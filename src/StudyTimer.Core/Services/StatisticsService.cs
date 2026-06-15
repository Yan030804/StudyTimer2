using System.Globalization;
using StudyTimer.Core.Models;
using StudyTimer.Core.Storage;

namespace StudyTimer.Core.Services;

public sealed class StatisticsService(StudyRecordRepository repository)
{
    public IReadOnlyList<ChartPoint> GetLastSevenDays(DateOnly today)
    {
        return Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(offset - 6))
            .Select(date => new ChartPoint(
                date == today ? "今天" : date.ToString("M/d", CultureInfo.InvariantCulture),
                repository.GetDuration(date)))
            .ToArray();
    }

    public IReadOnlyList<ChartPoint> GetWeeksOfMonth(int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var cursor = StartOfWeek(monthStart);
        var points = new List<ChartPoint>();
        var weekNumber = 1;

        while (cursor <= monthEnd)
        {
            var weekEnd = cursor.AddDays(6);
            var rangeStart = cursor < monthStart ? monthStart : cursor;
            var rangeEnd = weekEnd > monthEnd ? monthEnd : weekEnd;
            var duration = SumRange(rangeStart, rangeEnd);
            points.Add(new ChartPoint(
                $"第{weekNumber}周\n{rangeStart:M/d}-{rangeEnd:M/d}",
                duration));
            cursor = cursor.AddDays(7);
            weekNumber++;
        }

        return points;
    }

    public IReadOnlyList<ChartPoint> GetMonthsOfYear(int year)
    {
        return Enumerable.Range(1, 12)
            .Select(month => new ChartPoint(
                $"{month}月",
                SumRange(new DateOnly(year, month, 1), new DateOnly(year, month, 1).AddMonths(1).AddDays(-1))))
            .ToArray();
    }

    private TimeSpan SumRange(DateOnly start, DateOnly end)
    {
        long ticks = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            ticks += repository.GetDuration(date).Ticks;
        }

        return TimeSpan.FromTicks(ticks);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
