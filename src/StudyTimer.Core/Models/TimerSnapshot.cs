namespace StudyTimer.Core.Models;

public enum TimerStatus
{
    Stopped,
    Running,
    Paused
}

public sealed record TimerSnapshot(
    TimerStatus Status,
    DateTime? ActiveStartedAt,
    IReadOnlyList<StudySession> CompletedSegments,
    DateTime LastHeartbeat,
    Guid? ActiveSubjectId = null,
    string? ActiveSubjectName = null);
