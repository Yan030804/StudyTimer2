using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using StudyTimer.Core.Models;
using StudyTimer.Core.Services;

namespace StudyTimer.Core.Storage;

public sealed partial class StudyRecordRepository
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public StudyRecordRepository(string? rootPath = null)
    {
        RootPath = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "StudyTimerData");
        EnsureYearStructure(DateTime.Now.Year);
    }

    public string RootPath { get; }

    public void EnsureYearStructure(int year)
    {
        for (var month = 1; month <= 12; month++)
        {
            Directory.CreateDirectory(Path.Combine(
                RootPath,
                year.ToString("0000", CultureInfo.InvariantCulture),
                month.ToString("00", CultureInfo.InvariantCulture)));
        }
    }

    public string GetFilePath(DateOnly date) => Path.Combine(
        RootPath,
        date.Year.ToString("0000", CultureInfo.InvariantCulture),
        date.Month.ToString("00", CultureInfo.InvariantCulture),
        $"{date:yyyy-MM-dd}.txt");

    public DailyStudyRecord Load(DateOnly date)
    {
        var path = GetFilePath(date);
        if (!File.Exists(path))
        {
            return DailyStudyRecord.Empty(date);
        }

        var sessions = new List<StudySession>();
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        foreach (var line in lines)
        {
            var match = SessionLineRegex().Match(line.Trim());
            if (!match.Success)
            {
                continue;
            }

            if (!DateTime.TryParseExact(match.Groups["start"].Value, DateTimeFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                !DateTime.TryParseExact(match.Groups["end"].Value, DateTimeFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            {
                throw new InvalidDataException($"学习明细时间格式无效：{path}");
            }

            try
            {
                sessions.Add(new StudySession(start, end));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"学习明细结束时间必须晚于开始时间：{path}", exception);
            }
        }

        return new DailyStudyRecord(date, sessions.OrderBy(session => session.Start).ToArray());
    }

    public TimeSpan GetDuration(DateOnly date) => Load(date).TotalDuration;

    public void AddSessions(IEnumerable<StudySession> sessions)
    {
        var splitSessions = sessions.SelectMany(SplitByDay).ToArray();
        foreach (var group in splitSessions.GroupBy(session => DateOnly.FromDateTime(session.Start)))
        {
            var current = Load(group.Key);
            var combined = current.Sessions.Concat(group).OrderBy(session => session.Start).ToArray();
            Save(new DailyStudyRecord(group.Key, combined));
        }
    }

    public void Save(DailyStudyRecord record)
    {
        ValidateDailyRecord(record);
        EnsureYearStructure(record.Date.Year);
        var path = GetFilePath(record.Date);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("无法确定学习记录目录。");
        Directory.CreateDirectory(directory);

        var text = BuildText(record);
        var temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static IReadOnlyList<StudySession> SplitByDay(StudySession session)
    {
        var result = new List<StudySession>();
        var cursor = session.Start;
        while (cursor.Date < session.End.Date)
        {
            var midnight = cursor.Date.AddDays(1);
            result.Add(new StudySession(cursor, midnight));
            cursor = midnight;
        }

        if (session.End > cursor)
        {
            result.Add(new StudySession(cursor, session.End));
        }

        return result;
    }

    private static void ValidateDailyRecord(DailyStudyRecord record)
    {
        foreach (var session in record.Sessions)
        {
            var sessionDate = DateOnly.FromDateTime(session.Start);
            var endsAtNextMidnight = session.End.TimeOfDay == TimeSpan.Zero &&
                                     DateOnly.FromDateTime(session.End) == record.Date.AddDays(1);
            if (sessionDate != record.Date ||
                (DateOnly.FromDateTime(session.End) != record.Date && !endsAtNextMidnight))
            {
                throw new InvalidDataException("每日记录只能包含当天范围内的学习明细。");
            }
        }
    }

    private static string BuildText(DailyStudyRecord record)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"日期: {record.Date:yyyy-MM-dd}");
        builder.AppendLine($"总学习时长: {DurationFormatter.Clock(record.TotalDuration)}");
        builder.AppendLine($"总秒数: {(long)record.TotalDuration.TotalSeconds}");
        builder.AppendLine();
        builder.AppendLine("学习明细:");

        for (var index = 0; index < record.Sessions.Count; index++)
        {
            var session = record.Sessions[index];
            builder.AppendLine(
                $"{index + 1}. {session.Start.ToString(DateTimeFormat, CultureInfo.InvariantCulture)} -> " +
                $"{session.End.ToString(DateTimeFormat, CultureInfo.InvariantCulture)} | " +
                DurationFormatter.Clock(session.Duration));
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"^\d+\.\s+(?<start>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\s+->\s+(?<end>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\s+\|\s+\d{2,}:\d{2}:\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex SessionLineRegex();
}
