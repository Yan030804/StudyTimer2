using System.Text.Json;
using StudyTimer.Core.Models;

namespace StudyTimer.Core.Storage;

public sealed class AppSettingsStore
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public AppSettingsStore(string rootPath)
    {
        FilePath = Path.Combine(rootPath, ".settings.json");
    }

    public string FilePath { get; }

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return Normalize(new AppSettings());
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), _options)
            ?? new AppSettings();
        return Normalize(settings);
    }

    public void Save(AppSettings settings)
    {
        var normalized = Normalize(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporaryPath = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(normalized, _options));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        var subjects = settings.Subjects
            .Where(subject => subject.Id != SubjectDefinition.UncategorizedId)
            .GroupBy(subject => subject.Id)
            .Select(group => group.First())
            .ToList();
        subjects.Insert(0, SubjectDefinition.Uncategorized);

        return settings with
        {
            Subjects = subjects,
            LastSubjectId = subjects.Any(subject => subject.Id == settings.LastSubjectId && !subject.IsArchived)
                ? settings.LastSubjectId
                : SubjectDefinition.UncategorizedId
        };
    }
}
