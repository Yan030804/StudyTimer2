using StudyTimer.Core.Models;
using StudyTimer.Core.Services;
using StudyTimer.Core.Storage;

var runner = new TestRunner();
runner.Run("跨午夜记录会按日期拆分", TestMidnightSplit);
runner.Run("文本记录可以保存并重新读取", TestRepositoryRoundTrip);
runner.Run("暂停和恢复后的计时长度正确", TestTimerEngine);
runner.Run("异常恢复只计算到最后心跳", TestRecoverySnapshot);
runner.Run("恢复状态可以持久化并读取", TestRecoveryStore);
runner.Run("七天统计按日期聚合", TestSevenDayStatistics);
runner.Run("月内周统计只计算当月日期", TestWeeklyStatistics);
runner.Finish();

static void TestMidnightSplit()
{
    var session = new StudySession(
        new DateTime(2026, 6, 15, 23, 50, 0),
        new DateTime(2026, 6, 16, 0, 20, 0));
    var parts = StudyRecordRepository.SplitByDay(session);
    Assert.Equal(2, parts.Count);
    Assert.Equal(TimeSpan.FromMinutes(10), parts[0].Duration);
    Assert.Equal(TimeSpan.FromMinutes(20), parts[1].Duration);
}

static void TestRepositoryRoundTrip()
{
    var root = TestPaths.Create("round-trip");
    var repository = new StudyRecordRepository(root);
    var date = new DateOnly(2026, 6, 15);
    repository.Save(new DailyStudyRecord(date,
    [
        new StudySession(new DateTime(2026, 6, 15, 8, 0, 0), new DateTime(2026, 6, 15, 9, 15, 0)),
        new StudySession(new DateTime(2026, 6, 15, 14, 0, 0), new DateTime(2026, 6, 15, 14, 30, 0))
    ]));

    var loaded = repository.Load(date);
    Assert.Equal(2, loaded.Sessions.Count);
    Assert.Equal(TimeSpan.FromMinutes(105), loaded.TotalDuration);
    Assert.True(File.ReadAllText(repository.GetFilePath(date)).Contains("总秒数: 6300"));
}

static void TestTimerEngine()
{
    var engine = new StudyTimerEngine();
    var start = new DateTime(2026, 6, 15, 8, 0, 0);
    engine.Start(start);
    engine.Pause(start.AddMinutes(30));
    engine.Resume(start.AddMinutes(40));
    var result = engine.Stop(start.AddHours(1));
    Assert.Equal(2, result.Count);
    Assert.Equal(TimeSpan.FromMinutes(50), TimeSpan.FromTicks(result.Sum(item => item.Duration.Ticks)));
}

static void TestRecoverySnapshot()
{
    var engine = new StudyTimerEngine();
    var start = new DateTime(2026, 6, 15, 8, 0, 0);
    engine.Start(start);
    var snapshot = engine.CreateSnapshot(start.AddMinutes(25));

    var restored = new StudyTimerEngine();
    restored.Restore(snapshot);
    Assert.Equal(TimerStatus.Paused, restored.Status);
    Assert.Equal(TimeSpan.FromMinutes(25), restored.GetElapsed(start.AddHours(2)));
}

static void TestSevenDayStatistics()
{
    var root = TestPaths.Create("statistics");
    var repository = new StudyRecordRepository(root);
    var today = new DateOnly(2026, 6, 15);
    repository.Save(new DailyStudyRecord(today.AddDays(-1),
    [
        new StudySession(new DateTime(2026, 6, 14, 10, 0, 0), new DateTime(2026, 6, 14, 12, 0, 0))
    ]));

    var points = new StatisticsService(repository).GetLastSevenDays(today);
    Assert.Equal(7, points.Count);
    Assert.Equal(TimeSpan.FromHours(2), points[5].Duration);
    Assert.Equal("今天", points[6].Label);
}

static void TestRecoveryStore()
{
    var root = TestPaths.Create("recovery");
    var store = new TimerRecoveryStore(root);
    var snapshot = new TimerSnapshot(
        TimerStatus.Running,
        new DateTime(2026, 6, 15, 8, 0, 0),
        [new StudySession(new DateTime(2026, 6, 15, 7, 0, 0), new DateTime(2026, 6, 15, 7, 30, 0))],
        new DateTime(2026, 6, 15, 8, 20, 0));
    store.Save(snapshot);

    var loaded = store.Load();
    Assert.True(loaded is not null);
    Assert.Equal(snapshot.Status, loaded!.Status);
    Assert.Equal(snapshot.ActiveStartedAt, loaded.ActiveStartedAt);
    Assert.Equal(1, loaded.CompletedSegments.Count);
}

static void TestWeeklyStatistics()
{
    var root = TestPaths.Create("weekly-statistics");
    var repository = new StudyRecordRepository(root);
    repository.Save(new DailyStudyRecord(new DateOnly(2026, 6, 1),
    [
        new StudySession(new DateTime(2026, 6, 1, 8, 0, 0), new DateTime(2026, 6, 1, 9, 0, 0))
    ]));
    repository.Save(new DailyStudyRecord(new DateOnly(2026, 6, 8),
    [
        new StudySession(new DateTime(2026, 6, 8, 8, 0, 0), new DateTime(2026, 6, 8, 10, 0, 0))
    ]));

    var points = new StatisticsService(repository).GetWeeksOfMonth(2026, 6);
    Assert.Equal(5, points.Count);
    Assert.Equal(TimeSpan.FromHours(1), points[0].Duration);
    Assert.Equal(TimeSpan.FromHours(2), points[1].Duration);
}

static class TestPaths
{
    public static string Create(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", name);
        Directory.CreateDirectory(path);
        return path;
    }
}

static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"预期 {expected}，实际 {actual}。");
        }
    }

    public static void True(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("断言条件为 false。");
        }
    }
}

sealed class TestRunner
{
    private int _passed;
    private int _failed;

    public void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"[通过] {name}");
        }
        catch (Exception exception)
        {
            _failed++;
            Console.WriteLine($"[失败] {name}: {exception.Message}");
        }
    }

    public void Finish()
    {
        Console.WriteLine($"\n测试完成：{_passed} 通过，{_failed} 失败。");
        if (_failed > 0)
        {
            Environment.ExitCode = 1;
        }
    }
}
