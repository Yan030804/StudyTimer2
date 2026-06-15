using StudyTimer.Core.Models;
using StudyTimer.Core.Storage;

namespace StudyTimer.Core.Services;

public sealed class SubjectService
{
    public static readonly string[] ColorPalette =
    [
        "#4F46E5", "#2563EB", "#0891B2", "#059669",
        "#65A30D", "#D97706", "#E11D48", "#9333EA"
    ];

    private readonly AppSettingsStore _settingsStore;
    private readonly StudyRecordRepository _repository;

    public SubjectService(AppSettingsStore settingsStore, StudyRecordRepository repository)
    {
        _settingsStore = settingsStore;
        _repository = repository;
        Settings = settingsStore.Load();
    }

    public AppSettings Settings { get; private set; }

    public IReadOnlyList<SubjectDefinition> Subjects => Settings.Subjects;

    public IReadOnlyList<SubjectDefinition> ActiveSubjects => Settings.Subjects
        .Where(subject => !subject.IsArchived)
        .ToArray();

    public SubjectDefinition LastSubject =>
        ActiveSubjects.FirstOrDefault(subject => subject.Id == Settings.LastSubjectId)
        ?? SubjectDefinition.Uncategorized;

    public SubjectDefinition Add(string name, string color)
    {
        var normalizedName = ValidateName(name);
        EnsureUniqueName(normalizedName);
        var normalizedColor = ValidateColor(color);
        var subject = new SubjectDefinition(Guid.NewGuid(), normalizedName, normalizedColor);
        Settings.Subjects.Add(subject);
        Save();
        return subject;
    }

    public void Rename(Guid id, string name, string color)
    {
        if (id == SubjectDefinition.UncategorizedId)
        {
            throw new InvalidOperationException("内置科目“未分类”不能修改。");
        }

        var current = Find(id);
        var normalizedName = ValidateName(name);
        EnsureUniqueName(normalizedName, id);
        var updated = current with { Name = normalizedName, Color = ValidateColor(color) };
        Replace(updated);
        _repository.RenameSubject(id, normalizedName);
        Save();
    }

    public void SetArchived(Guid id, bool archived)
    {
        if (id == SubjectDefinition.UncategorizedId)
        {
            throw new InvalidOperationException("内置科目“未分类”不能归档。");
        }

        var updated = Find(id) with { IsArchived = archived };
        Replace(updated);
        if (archived && Settings.LastSubjectId == id)
        {
            Settings.LastSubjectId = SubjectDefinition.UncategorizedId;
        }

        Save();
    }

    public void SetLastSubject(Guid id)
    {
        var subject = ActiveSubjects.FirstOrDefault(item => item.Id == id)
            ?? SubjectDefinition.Uncategorized;
        Settings.LastSubjectId = subject.Id;
        Save();
    }

    public void UpdateCompactWindow(double left, double top, bool topmost)
    {
        Settings.CompactLeft = left;
        Settings.CompactTop = top;
        Settings.CompactTopmost = topmost;
        Save();
    }

    public void UpdateCompactTopmost(bool topmost)
    {
        Settings.CompactTopmost = topmost;
        Save();
    }

    private SubjectDefinition Find(Guid id) => Settings.Subjects.FirstOrDefault(subject => subject.Id == id)
        ?? throw new InvalidOperationException("科目不存在。");

    private void Replace(SubjectDefinition updated)
    {
        var index = Settings.Subjects.FindIndex(subject => subject.Id == updated.Id);
        Settings.Subjects[index] = updated;
    }

    private void EnsureUniqueName(string name, Guid? excludingId = null)
    {
        if (Settings.Subjects.Any(subject => subject.Id != excludingId &&
            string.Equals(subject.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("科目名称已存在。");
        }
    }

    private static string ValidateName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length is < 1 or > 24)
        {
            throw new InvalidOperationException("科目名称长度应为 1 至 24 个字符。");
        }

        if (normalized.Contains('|') || normalized.Contains('\r') || normalized.Contains('\n'))
        {
            throw new InvalidOperationException("科目名称不能包含竖线或换行。");
        }

        return normalized;
    }

    private static string ValidateColor(string color)
    {
        if (!ColorPalette.Contains(color, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("请选择预设科目颜色。");
        }

        return color.ToUpperInvariant();
    }

    private void Save() => _settingsStore.Save(Settings);
}
