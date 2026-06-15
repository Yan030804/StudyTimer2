using System.Text;
using StudyTimer.Core.Models;
using StudyTimer.Core.Services;
using StudyTimer.Core.Storage;

var runner = new TestRunner();
runner.Run("跨午夜记录按日期拆分并保留科目", TestMidnightSplitKeepsSubject);
runner.Run("新文本记录可保存并重新读取", TestRepositoryRoundTrip);
runner.Run("旧文本记录映射为未分类", TestLegacyRecordCompatibility);
runner.Run("手工编辑的无效行不会破坏有效记录", TestHandEditedRecordCompatibility);
runner.Run("科目新增判重改名归档恢复", TestSubjectLifecycle);
runner.Run("计时暂停恢复全过程保留科目", TestTimerEngineSubject);
runner.Run("异常恢复保留科目并只计算到心跳", TestRecoverySnapshotSubject);
runner.Run("七天统计支持科目筛选和四项摘要", TestSevenDayStatisticsSummary);
runner.Run("当前月平均值不计入未来日期", TestCurrentMonthEffectiveRange);
runner.Run("历史月按周统计且周一为起点", TestWeeklyStatistics);
runner.Run("年度统计覆盖闰年和十二个月", TestYearStatistics);
runner.Run("统计周期导航步长正确", TestStatisticsNavigation);
runner.Run("CSV 导出包含 BOM 并正确转义", TestCsvExport);
runner.Run("设置可持久化科目与紧凑窗口状态", TestSettingsPersistence);
runner.Finish();

static void TestMidnightSplitKeepsSubject()
{
    var subject = NewSubject("数学");
    var session = Session(new DateTime(2026, 6, 15, 23, 50, 0),
        new DateTime(2026, 6, 16, 0, 20, 0), subject);
    var parts = StudyRecordRepository.SplitByDay(session);
    Assert.Equal(2, parts.Count);
    Assert.Equal(TimeSpan.FromMinutes(10), parts[0].Duration);
    Assert.Equal(TimeSpan.FromMinutes(20), parts[1].Duration);
    Assert.True(parts.All(item => item.SubjectId == subject.Id && item.SubjectName == subject.Name));
}

static void TestRepositoryRoundTrip()
{
    var repository = NewRepository("round-trip");
    var date = new DateOnly(2026, 6, 15);
    var subject = NewSubject("英语");
    repository.Save(new DailyStudyRecord(date,
    [
        Session(new DateTime(2026, 6, 15, 8, 0, 0), new DateTime(2026, 6, 15, 9, 15, 0), subject),
        Session(new DateTime(2026, 6, 15, 14, 0, 0), new DateTime(2026, 6, 15, 14, 30, 0), subject)
    ]));

    var loaded = repository.Load(date);
    Assert.Equal(2, loaded.Sessions.Count);
    Assert.Equal(TimeSpan.FromMinutes(105), loaded.TotalDuration);
    Assert.True(loaded.Sessions.All(item => item.SubjectId == subject.Id && item.SubjectName == "英语"));
    var text = File.ReadAllText(repository.GetFilePath(date));
    Assert.Contains("科目: 英语", text);
    Assert.Contains($"科目ID: {subject.Id:D}", text);
    Assert.Contains("总秒数: 6300", text);
}

static void TestLegacyRecordCompatibility()
{
    var repository = NewRepository("legacy");
    var date = new DateOnly(2026, 5, 2);
    var path = repository.GetFilePath(date);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path,
        "日期: 2026-05-02\n总学习时长: 01:00:00\n总秒数: 3600\n\n学习明细:\n1. 2026-05-02 08:00:00 -> 2026-05-02 09:00:00 | 01:00:00\n");

    var loaded = repository.Load(date);
    Assert.Equal(1, loaded.Sessions.Count);
    Assert.Equal(SubjectDefinition.UncategorizedId, loaded.Sessions[0].SubjectId);
    Assert.Equal(SubjectDefinition.UncategorizedName, loaded.Sessions[0].SubjectName);
}

static void TestHandEditedRecordCompatibility()
{
    var repository = NewRepository("hand-edited");
    var date = new DateOnly(2026, 6, 3);
    var subject = NewSubject("物理");
    var path = repository.GetFilePath(date);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path,
        $"随手写的备注\n1. 2026-06-03 09:00:00 -> 2026-06-03 10:20:00 | 01:20:00 | 科目: 物理 | 科目ID: {subject.Id:D}\n2. 这不是有效记录\n");

    var loaded = repository.Load(date);
    Assert.Equal(1, loaded.Sessions.Count);
    Assert.Equal(subject.Id, loaded.Sessions[0].SubjectId);
    Assert.Equal(TimeSpan.FromMinutes(80), loaded.TotalDuration);
}

static void TestSubjectLifecycle()
{
    var root = TestPaths.Create("subjects");
    var repository = new StudyRecordRepository(root);
    var service = new SubjectService(new AppSettingsStore(root), repository);
    var subject = service.Add(" 数学 ", SubjectService.ColorPalette[0]);
    Assert.Equal("数学", subject.Name);
    Assert.Throws<InvalidOperationException>(() => service.Add("数学", SubjectService.ColorPalette[1]));
    Assert.Throws<InvalidOperationException>(() => service.Add("数|学", SubjectService.ColorPalette[1]));

    var date = new DateOnly(2026, 6, 4);
    repository.Save(new DailyStudyRecord(date,
    [
        Session(new DateTime(2026, 6, 4, 8, 0, 0), new DateTime(2026, 6, 4, 9, 0, 0), subject)
    ]));
    service.Rename(subject.Id, "高等数学", SubjectService.ColorPalette[2]);
    Assert.Equal("高等数学", repository.Load(date).Sessions.Single().SubjectName);

    service.SetLastSubject(subject.Id);
    service.SetArchived(subject.Id, true);
    Assert.True(service.ActiveSubjects.All(item => item.Id != subject.Id));
    Assert.Equal(SubjectDefinition.UncategorizedId, service.LastSubject.Id);
    Assert.True(repository.HasSubjectHistory(subject.Id));
    service.SetArchived(subject.Id, false);
    Assert.True(service.ActiveSubjects.Any(item => item.Id == subject.Id));
    Assert.Throws<InvalidOperationException>(() => service.SetArchived(SubjectDefinition.UncategorizedId, true));
}

static void TestTimerEngineSubject()
{
    var subject = NewSubject("编程");
    var engine = new StudyTimerEngine();
    var start = new DateTime(2026, 6, 15, 8, 0, 0);
    engine.Start(start, subject);
    engine.Pause(start.AddMinutes(30));
    engine.Resume(start.AddMinutes(40));
    var result = engine.Stop(start.AddHours(1));
    Assert.Equal(2, result.Count);
    Assert.Equal(TimeSpan.FromMinutes(50), TimeSpan.FromTicks(result.Sum(item => item.Duration.Ticks)));
    Assert.True(result.All(item => item.SubjectId == subject.Id && item.SubjectName == subject.Name));
}

static void TestRecoverySnapshotSubject()
{
    var subject = NewSubject("英语");
    var engine = new StudyTimerEngine();
    var start = new DateTime(2026, 6, 15, 8, 0, 0);
    engine.Start(start, subject);
    var snapshot = engine.CreateSnapshot(start.AddMinutes(25));

    var root = TestPaths.Create("recovery");
    var store = new TimerRecoveryStore(root);
    store.Save(snapshot);
    var loaded = store.Load();
    Assert.True(loaded is not null);

    var restored = new StudyTimerEngine();
    restored.Restore(loaded!);
    Assert.Equal(TimerStatus.Paused, restored.Status);
    Assert.Equal(TimeSpan.FromMinutes(25), restored.GetElapsed(start.AddHours(2)));
    Assert.Equal(subject.Id, restored.ActiveSubject.Id);
    Assert.Equal(subject.Name, restored.ActiveSubject.Name);
    Assert.True(restored.GetSegments(start.AddHours(2)).All(item => item.SubjectId == subject.Id));
}

static void TestSevenDayStatisticsSummary()
{
    var repository = NewRepository("statistics-summary");
    var today = new DateOnly(2026, 6, 15);
    var math = NewSubject("数学");
    var english = NewSubject("英语");
    SaveHours(repository, today.AddDays(-4), math, 1);
    SaveHours(repository, today.AddDays(-3), math, 2);
    SaveHours(repository, today.AddDays(-2), math, 3);
    SaveHours(repository, today.AddDays(-2), english, 1);

    var report = new StatisticsService(repository)
        .GetReport(StatisticsPeriod.SevenDays, today, today, math.Id);
    Assert.Equal(7, report.Points.Count);
    Assert.Equal(TimeSpan.FromHours(6), report.Summary.TotalDuration);
    Assert.Equal(TimeSpan.FromTicks(TimeSpan.FromHours(6).Ticks / 7), report.Summary.AveragePerCalendarDay);
    Assert.Equal(today.AddDays(-2), report.Summary.LongestDay);
    Assert.Equal(TimeSpan.FromHours(3), report.Summary.LongestDayDuration);
    Assert.Equal(3, report.Summary.LongestStreakDays);
}

static void TestCurrentMonthEffectiveRange()
{
    var repository = NewRepository("current-month");
    var today = new DateOnly(2026, 6, 15);
    SaveHours(repository, new DateOnly(2026, 6, 1), NewSubject("数学"), 15);
    var report = new StatisticsService(repository)
        .GetReport(StatisticsPeriod.Month, today, today);
    Assert.Equal(new DateOnly(2026, 6, 30), report.PlotEnd);
    Assert.Equal(today, report.EffectiveEnd);
    Assert.Equal(TimeSpan.FromHours(1), report.Summary.AveragePerCalendarDay);
}

static void TestWeeklyStatistics()
{
    var repository = NewRepository("weekly-statistics");
    var subject = NewSubject("数学");
    SaveHours(repository, new DateOnly(2026, 6, 1), subject, 1);
    SaveHours(repository, new DateOnly(2026, 6, 8), subject, 2);
    var points = new StatisticsService(repository).GetWeeksOfMonth(2026, 6);
    Assert.Equal(5, points.Count);
    Assert.Equal(TimeSpan.FromHours(1), points[0].Duration);
    Assert.Equal(TimeSpan.FromHours(2), points[1].Duration);
}

static void TestYearStatistics()
{
    var repository = NewRepository("year-statistics");
    var subject = NewSubject("数学");
    SaveHours(repository, new DateOnly(2024, 2, 29), subject, 2);
    SaveHours(repository, new DateOnly(2024, 12, 31), subject, 3);
    var report = new StatisticsService(repository)
        .GetReport(StatisticsPeriod.Year, new DateOnly(2024, 6, 1), new DateOnly(2026, 6, 15));
    Assert.Equal(12, report.Points.Count);
    Assert.Equal(TimeSpan.FromHours(2), report.Points[1].Duration);
    Assert.Equal(TimeSpan.FromHours(3), report.Points[11].Duration);
    Assert.Equal(TimeSpan.FromHours(5), report.Summary.TotalDuration);
    Assert.Equal(1, report.Summary.LongestStreakDays);
}

static void TestStatisticsNavigation()
{
    var anchor = new DateOnly(2026, 6, 15);
    Assert.Equal(anchor.AddDays(7), StatisticsService.MoveAnchor(StatisticsPeriod.SevenDays, anchor, 1));
    Assert.Equal(anchor.AddMonths(-1), StatisticsService.MoveAnchor(StatisticsPeriod.Month, anchor, -1));
    Assert.Equal(anchor.AddYears(1), StatisticsService.MoveAnchor(StatisticsPeriod.Year, anchor, 1));
}

static void TestCsvExport()
{
    var repository = NewRepository("csv");
    var subject = NewSubject("英语,\"阅读\"");
    var date = new DateOnly(2026, 6, 12);
    repository.Save(new DailyStudyRecord(date,
    [
        Session(new DateTime(2026, 6, 12, 8, 0, 0), new DateTime(2026, 6, 12, 9, 30, 0), subject)
    ]));
    var path = TestPaths.File("csv", "export.csv");
    new CsvExportService(repository).ExportRange(path, date, date, subject.Id);
    var bytes = File.ReadAllBytes(path);
    Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    var text = Encoding.UTF8.GetString(bytes);
    Assert.Contains("日期,科目,开始时间,结束时间,学习时长,秒数", text);
    Assert.Contains("\"英语,\"\"阅读\"\"\"", text);
    Assert.Contains(",5400", text);
}

static void TestSettingsPersistence()
{
    var root = TestPaths.Create("settings");
    var repository = new StudyRecordRepository(root);
    var store = new AppSettingsStore(root);
    var service = new SubjectService(store, repository);
    var subject = service.Add("化学", SubjectService.ColorPalette[3]);
    service.SetLastSubject(subject.Id);
    service.UpdateCompactWindow(123.5, 456.25, false);

    var reloaded = new SubjectService(store, repository);
    Assert.Equal(subject.Id, reloaded.LastSubject.Id);
    Assert.Equal(123.5, reloaded.Settings.CompactLeft);
    Assert.Equal(456.25, reloaded.Settings.CompactTop);
    Assert.Equal(false, reloaded.Settings.CompactTopmost);
    Assert.True(File.Exists(store.FilePath));
}

static SubjectDefinition NewSubject(string name) =>
    new(Guid.NewGuid(), name, SubjectService.ColorPalette[0]);

static StudySession Session(DateTime start, DateTime end, SubjectDefinition subject) =>
    new(start, end, subject.Id, subject.Name);

static StudyRecordRepository NewRepository(string name) => new(TestPaths.Create(name));

static void SaveHours(StudyRecordRepository repository, DateOnly date, SubjectDefinition subject, int hours)
{
    var start = date.ToDateTime(new TimeOnly(8, 0));
    repository.AddSessions([Session(start, start.AddHours(hours), subject)]);
}

static class TestPaths
{
    private static readonly string RunId = Guid.NewGuid().ToString("N");

    public static string Create(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", RunId, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string File(string group, string name)
    {
        var path = Create(group);
        return Path.Combine(path, name);
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

    public static void Contains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"文本中未找到：{expected}");
        }
    }

    public static void Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"预期抛出 {typeof(TException).Name}。");
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
