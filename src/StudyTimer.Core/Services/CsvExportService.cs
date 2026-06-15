using System.Globalization;
using System.Text;
using StudyTimer.Core.Models;
using StudyTimer.Core.Storage;

namespace StudyTimer.Core.Services;

public sealed class CsvExportService(StudyRecordRepository repository)
{
    public void ExportRange(string path, DateOnly start, DateOnly end, Guid? subjectId)
    {
        var sessions = repository.LoadRange(start, end)
            .SelectMany(record => StudyRecordRepository.FilterSessions(record.Sessions, subjectId));
        Write(path, sessions);
    }

    public void ExportAll(string path)
    {
        Write(path, repository.LoadAll().SelectMany(record => record.Sessions));
    }

    private static void Write(string path, IEnumerable<StudySession> sessions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("日期,科目,开始时间,结束时间,学习时长,秒数");
        foreach (var session in sessions.OrderBy(item => item.Start))
        {
            builder.AppendLine(string.Join(",",
                Escape(session.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Escape(session.SubjectName),
                Escape(session.Start.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
                Escape(session.End.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
                Escape(DurationFormatter.Clock(session.Duration)),
                ((long)session.Duration.TotalSeconds).ToString(CultureInfo.InvariantCulture)));
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
