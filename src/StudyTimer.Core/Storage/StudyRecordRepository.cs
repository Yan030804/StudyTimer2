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
        foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
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

            var subjectId = SubjectDefinition.UncategorizedId;
            var subjectName = SubjectDefinition.UncategorizedName;
            if (match.Groups["subjectName"].Success || match.Groups["subjectId"].Success)
            {
                if (!match.Groups["subjectName"].Success ||
                    !Guid.TryParse(match.Groups["subjectId"].Value, out subjectId))
                {
                    throw new InvalidDataException($"学习明细科目格式无效：{path}");
                }

                subjectName = match.Groups["subjectName"].Value.Trim();
                if (string.IsNullOrWhiteSpace(subjectName))
                {
                    throw new InvalidDataException($"学习明细科目名称为空：{path}");
                }
            }

            try
            {
                sessions.Add(new StudySession(start, end, subjectId, subjectName));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"学习明细结束时间必须晚于开始时间：{path}", exception);
            }
        }

        return new DailyStudyRecord(date, sessions.OrderBy(session => session.Start).ToArray());
    }

    public IReadOnlyList<DailyStudyRecord> LoadRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            return Array.Empty<DailyStudyRecord>();
        }

        var records = new List<DailyStudyRecord>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            records.Add(Load(date));
        }

        return records;
    }

    public IReadOnlyList<DailyStudyRecord> LoadAll()
    {
        if (!Directory.Exists(RootPath))
        {
            return Array.Empty<DailyStudyRecord>();
        }

        return Directory.EnumerateFiles(RootPath, "*.txt", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
            .Select(name => DateOnly.ParseExact(name!, "yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(date => date)
            .Select(Load)
            .ToArray();
    }

    public TimeSpan GetDuration(DateOnly date, Guid? subjectId = null)
    {
        var sessions = FilterSessions(Load(date).Sessions, subjectId);
        return TimeSpan.FromTicks(sessions.Sum(session => session.Duration.Ticks));
    }

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

    public void RenameSubject(Guid subjectId, string newName)
    {
        foreach (var record in LoadAll().Where(record => record.Sessions.Any(session => session.SubjectId == subjectId)))
        {
            var updated = record.Sessions
                .Select(session => session.SubjectId == subjectId
                    ? new StudySession(session.Start, session.End, subjectId, newName)
                    : session)
                .ToArray();
            Save(new DailyStudyRecord(record.Date, updated));
        }
    }

    public bool HasSubjectHistory(Guid subjectId) =>
        LoadAll().Any(record => record.Sessions.Any(session => session.SubjectId == subjectId));

    public void Save(DailyStudyRecord record)
    {
        ValidateDailyRecord(record);
        EnsureYearStructure(record.Date.Year);
        var path = GetFilePath(record.Date);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("无法确定学习记录目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, BuildText(record), new UTF8Encoding(false));
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
            result.Add(new StudySession(cursor, midnight, session.SubjectId, session.SubjectName));
            cursor = midnight;
        }

        if (session.End > cursor)
        {
            result.Add(new StudySession(cursor, session.End, session.SubjectId, session.SubjectName));
        }

        return result;
    }

    public static IEnumerable<StudySession> FilterSessions(
        IEnumerable<StudySession> sessions,
        Guid? subjectId) =>
        subjectId is null ? sessions : sessions.Where(session => session.SubjectId == subjectId.Value);

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

            if (session.SubjectName.Contains('|') || session.SubjectName.Contains('\r') || session.SubjectName.Contains('\n'))
            {
                throw new InvalidDataException("科目名称不能包含竖线或换行。");
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
                $"{DurationFormatter.Clock(session.Duration)} | 科目: {session.SubjectName} | 科目ID: {session.SubjectId:D}");
        }

        return builder.ToString();
    }

    [GeneratedRegex(
        @"^\d+\.\s+(?<start>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\s+->\s+(?<end>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\s+\|\s+\d{2,}:\d{2}:\d{2}(?:\s+\|\s+科目:\s*(?<subjectName>[^|\r\n]+?)\s+\|\s+科目ID:\s*(?<subjectId>[0-9a-fA-F-]{36}))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SessionLineRegex();
}
