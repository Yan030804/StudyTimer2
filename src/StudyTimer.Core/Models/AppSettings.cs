namespace StudyTimer.Core.Models;

public sealed record AppSettings
{
    public List<SubjectDefinition> Subjects { get; init; } = [SubjectDefinition.Uncategorized];

    public Guid LastSubjectId { get; set; } = SubjectDefinition.UncategorizedId;

    public double? CompactLeft { get; set; }

    public double? CompactTop { get; set; }

    public bool CompactTopmost { get; set; } = true;
}
