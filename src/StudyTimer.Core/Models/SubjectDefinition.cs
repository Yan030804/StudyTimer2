namespace StudyTimer.Core.Models;

public sealed record SubjectDefinition(
    Guid Id,
    string Name,
    string Color,
    bool IsArchived = false,
    bool IsBuiltIn = false)
{
    public static readonly Guid UncategorizedId = Guid.Empty;
    public const string UncategorizedName = "未分类";
    public const string UncategorizedColor = "#667085";

    public static SubjectDefinition Uncategorized { get; } = new(
        UncategorizedId,
        UncategorizedName,
        UncategorizedColor,
        IsArchived: false,
        IsBuiltIn: true);
}
