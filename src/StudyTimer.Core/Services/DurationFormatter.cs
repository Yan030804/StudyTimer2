namespace StudyTimer.Core.Services;

public static class DurationFormatter
{
    public static string Clock(TimeSpan duration)
    {
        var totalHours = (long)Math.Floor(Math.Max(0, duration.TotalHours));
        return $"{totalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    public static string Friendly(TimeSpan duration)
    {
        var totalHours = (long)Math.Floor(Math.Max(0, duration.TotalHours));
        return totalHours > 0
            ? $"{totalHours} 小时 {duration.Minutes} 分钟"
            : $"{duration.Minutes} 分钟";
    }
}
