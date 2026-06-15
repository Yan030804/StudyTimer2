using StudyTimer.Core.Models;

namespace StudyTimer.Core.Services;

public sealed class StudyTimerEngine
{
    private readonly List<StudySession> _completedSegments = [];
    private DateTime? _activeStartedAt;

    public TimerStatus Status { get; private set; } = TimerStatus.Stopped;

    public void Start(DateTime now)
    {
        if (Status != TimerStatus.Stopped)
        {
            throw new InvalidOperationException("The timer has already started.");
        }

        _completedSegments.Clear();
        _activeStartedAt = now;
        Status = TimerStatus.Running;
    }

    public void Pause(DateTime now)
    {
        if (Status != TimerStatus.Running || _activeStartedAt is null)
        {
            throw new InvalidOperationException("Only a running timer can be paused.");
        }

        if (now > _activeStartedAt.Value)
        {
            _completedSegments.Add(new StudySession(_activeStartedAt.Value, now));
        }

        _activeStartedAt = null;
        Status = TimerStatus.Paused;
    }

    public void Resume(DateTime now)
    {
        if (Status != TimerStatus.Paused)
        {
            throw new InvalidOperationException("Only a paused timer can be resumed.");
        }

        _activeStartedAt = now;
        Status = TimerStatus.Running;
    }

    public IReadOnlyList<StudySession> Stop(DateTime now)
    {
        if (Status == TimerStatus.Stopped)
        {
            return Array.Empty<StudySession>();
        }

        if (Status == TimerStatus.Running && _activeStartedAt is not null && now > _activeStartedAt.Value)
        {
            _completedSegments.Add(new StudySession(_activeStartedAt.Value, now));
        }

        var result = _completedSegments.ToArray();
        Reset();
        return result;
    }

    public void Reset()
    {
        _completedSegments.Clear();
        _activeStartedAt = null;
        Status = TimerStatus.Stopped;
    }

    public TimeSpan GetElapsed(DateTime now)
    {
        var ticks = _completedSegments.Sum(segment => segment.Duration.Ticks);
        if (Status == TimerStatus.Running && _activeStartedAt is not null && now > _activeStartedAt.Value)
        {
            ticks += (now - _activeStartedAt.Value).Ticks;
        }

        return TimeSpan.FromTicks(ticks);
    }

    public IReadOnlyList<StudySession> GetSegments(DateTime now)
    {
        var segments = new List<StudySession>(_completedSegments);
        if (Status == TimerStatus.Running && _activeStartedAt is not null && now > _activeStartedAt.Value)
        {
            segments.Add(new StudySession(_activeStartedAt.Value, now));
        }

        return segments;
    }

    public TimerSnapshot CreateSnapshot(DateTime heartbeat) =>
        new(Status, _activeStartedAt, _completedSegments.ToArray(), heartbeat);

    public void Restore(TimerSnapshot snapshot)
    {
        Reset();
        _completedSegments.AddRange(snapshot.CompletedSegments);

        if (snapshot.Status == TimerStatus.Running &&
            snapshot.ActiveStartedAt is not null &&
            snapshot.LastHeartbeat > snapshot.ActiveStartedAt.Value)
        {
            _completedSegments.Add(new StudySession(snapshot.ActiveStartedAt.Value, snapshot.LastHeartbeat));
        }

        Status = TimerStatus.Paused;
    }
}
