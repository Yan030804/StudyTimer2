using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using StudyTimer.Core.Models;
using StudyTimer.Core.Services;
using StudyTimer.Core.Storage;

namespace StudyTimer.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly StudyRecordRepository _repository;
    private readonly TimerRecoveryStore _recoveryStore;
    private readonly StatisticsService _statisticsService;
    private readonly CsvExportService _csvExportService;
    private readonly SubjectService _subjectService;
    private readonly StudyTimerEngine _engine = new();
    private readonly DispatcherTimer _uiTimer;
    private DateOnly _currentDate = DateOnly.FromDateTime(DateTime.Now);
    private TimeSpan _savedTodayDuration;
    private DateTime _historyDate = DateTime.Today;
    private DateTime _statisticsAnchorDate = DateTime.Today;
    private StatisticsPeriod _statisticsPeriod = StatisticsPeriod.SevenDays;
    private StatisticsReport? _statisticsReport;
    private SubjectDefinition _selectedSubject = SubjectDefinition.Uncategorized;
    private SubjectFilterOption _selectedStatisticsSubject = SubjectFilterOption.All;
    private SubjectFilterOption _selectedHistorySubject = SubjectFilterOption.All;
    private string _timerText = "00:00:00";
    private string _todayText = "0 分钟";
    private string _statusText = "准备开始";
    private string _toggleText = "开始学习";
    private int _heartbeatTicks;

    public MainViewModel(
        StudyRecordRepository repository,
        TimerRecoveryStore recoveryStore,
        SubjectService subjectService)
    {
        _repository = repository;
        _recoveryStore = recoveryStore;
        _subjectService = subjectService;
        _statisticsService = new StatisticsService(repository);
        _csvExportService = new CsvExportService(repository);
        _selectedSubject = _subjectService.LastSubject;

        ToggleTimerCommand = new RelayCommand(_ => ToggleTimer());
        StopTimerCommand = new RelayCommand(_ => StopAndSave(), _ => Status != TimerStatus.Stopped);
        OpenDataFolderCommand = new RelayCommand(_ => OpenDataFolder());
        CompactCommand = new RelayCommand(_ => CompactRequested?.Invoke());
        ManageSubjectsCommand = new RelayCommand(_ => ManageSubjectsRequested?.Invoke());
        RefreshCommand = new RelayCommand(_ => RefreshAll());

        _uiTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _uiTimer.Tick += (_, _) => Tick();
        _uiTimer.Start();

        ReloadSubjects();
        RefreshAll();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? ErrorOccurred;
    public event Action? CompactRequested;
    public event Action? ManageSubjectsRequested;

    public ObservableCollection<SubjectDefinition> ActiveSubjects { get; } = [];
    public ObservableCollection<SubjectFilterOption> StatisticsSubjectOptions { get; } = [];
    public ObservableCollection<SubjectFilterOption> HistorySubjectOptions { get; } = [];
    public ObservableCollection<SessionRow> HistorySessions { get; } = [];

    public ICommand ToggleTimerCommand { get; }
    public ICommand StopTimerCommand { get; }
    public ICommand OpenDataFolderCommand { get; }
    public ICommand CompactCommand { get; }
    public ICommand ManageSubjectsCommand { get; }
    public ICommand RefreshCommand { get; }

    public TimerStatus Status => _engine.Status;
    public string DataRootPath => _repository.RootPath;
    public SubjectService SubjectService => _subjectService;
    public bool CanSelectSubject => Status == TimerStatus.Stopped;
    public bool HasHistoryRecords => HistorySessions.Count > 0;
    public bool HasNoHistoryRecords => !HasHistoryRecords;
    public bool CanMoveHistoryNext => DateOnly.FromDateTime(HistoryDate) < DateOnly.FromDateTime(DateTime.Today);

    public string TimerText
    {
        get => _timerText;
        private set => SetField(ref _timerText, value);
    }

    public string TodayText
    {
        get => _todayText;
        private set => SetField(ref _todayText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string ToggleText
    {
        get => _toggleText;
        private set => SetField(ref _toggleText, value);
    }

    public SubjectDefinition SelectedSubject
    {
        get => _selectedSubject;
        set
        {
            if (!CanSelectSubject || value is null || !SetField(ref _selectedSubject, value))
            {
                return;
            }

            _subjectService.SetLastSubject(value.Id);
            NotifyCurrentSubjectProperties();
        }
    }

    public string CurrentSubjectName => Status == TimerStatus.Stopped
        ? SelectedSubject.Name
        : _engine.ActiveSubject.Name;

    public string CurrentSubjectColor => ResolveSubjectColor(CurrentSubjectId);

    public string CurrentSubjectBackground => SubjectColorHelper.SoftBackground(CurrentSubjectColor);

    public DateTime HistoryDate
    {
        get => _historyDate;
        set
        {
            var normalized = value.Date > DateTime.Today ? DateTime.Today : value.Date;
            if (SetField(ref _historyDate, normalized))
            {
                RefreshHistory();
                OnPropertyChanged(nameof(CanMoveHistoryNext));
            }
        }
    }

    public SubjectFilterOption SelectedHistorySubject
    {
        get => _selectedHistorySubject;
        set
        {
            if (value is not null && SetField(ref _selectedHistorySubject, value))
            {
                RefreshHistory();
            }
        }
    }

    public string HistoryTotalText { get; private set; } = "0 分钟";
    public string HistoryCountText { get; private set; } = "0 次";

    public StatisticsPeriod StatisticsPeriod
    {
        get => _statisticsPeriod;
        private set => SetField(ref _statisticsPeriod, value);
    }

    public DateTime StatisticsAnchorDate
    {
        get => _statisticsAnchorDate;
        set
        {
            var normalized = value.Date > DateTime.Today ? DateTime.Today : value.Date;
            if (SetField(ref _statisticsAnchorDate, normalized))
            {
                RefreshStatistics();
            }
        }
    }

    public SubjectFilterOption SelectedStatisticsSubject
    {
        get => _selectedStatisticsSubject;
        set
        {
            if (value is not null && SetField(ref _selectedStatisticsSubject, value))
            {
                RefreshStatistics();
            }
        }
    }

    public IReadOnlyList<ChartPoint> StatisticsPoints => _statisticsReport?.Points ?? Array.Empty<ChartPoint>();
    public IReadOnlyList<SubjectSharePoint> StatisticsSubjectShares =>
        _statisticsReport?.SubjectShares ?? Array.Empty<SubjectSharePoint>();
    public string StatisticsTitle => _statisticsReport?.Title ?? string.Empty;
    public string StatisticsTotalText => FormatMetric(_statisticsReport?.Summary.TotalDuration ?? TimeSpan.Zero);
    public string StatisticsAverageText => FormatMetric(_statisticsReport?.Summary.AveragePerCalendarDay ?? TimeSpan.Zero);
    public string StatisticsLongestDayText => _statisticsReport?.Summary.LongestDay is { } date
        ? $"{date:M月d日} · {FormatMetric(_statisticsReport.Summary.LongestDayDuration)}"
        : "暂无记录";
    public string StatisticsStreakText => $"{_statisticsReport?.Summary.LongestStreakDays ?? 0} 天";
    public bool CanNavigateStatisticsNext
    {
        get
        {
            var next = StatisticsService.MoveAnchor(StatisticsPeriod,
                DateOnly.FromDateTime(StatisticsAnchorDate), 1);
            var (start, _, _) = StatisticsService.GetPlotRange(StatisticsPeriod, next);
            return start <= DateOnly.FromDateTime(DateTime.Today);
        }
    }

    public string StatisticsAccessibleSummary =>
        $"{StatisticsTitle}，总时长 {StatisticsTotalText}，日均 {StatisticsAverageText}，" +
        $"最长学习日 {StatisticsLongestDayText}，最长连续 {StatisticsStreakText}";

    public void SetStatisticsPeriod(StatisticsPeriod period)
    {
        StatisticsPeriod = period;
        StatisticsAnchorDate = DateTime.Today;
        RefreshStatistics();
    }

    public void NavigateStatistics(int direction)
    {
        if (direction > 0 && !CanNavigateStatisticsNext)
        {
            return;
        }

        var next = StatisticsService.MoveAnchor(StatisticsPeriod,
            DateOnly.FromDateTime(StatisticsAnchorDate), direction);
        StatisticsAnchorDate = next.ToDateTime(TimeOnly.MinValue);
    }

    public void GoToCurrentStatisticsPeriod() => StatisticsAnchorDate = DateTime.Today;

    public void NavigateHistory(int direction)
    {
        if (direction > 0 && !CanMoveHistoryNext)
        {
            return;
        }

        HistoryDate = HistoryDate.AddDays(direction);
    }

    public void GoToTodayHistory() => HistoryDate = DateTime.Today;

    public void ExportCurrentRange(string path)
    {
        if (_statisticsReport is null)
        {
            return;
        }

        _csvExportService.ExportRange(path,
            _statisticsReport.EffectiveStart,
            _statisticsReport.EffectiveEnd,
            SelectedStatisticsSubject.SubjectId);
    }

    public void ExportAll(string path) => _csvExportService.ExportAll(path);

    public void ReloadSubjects()
    {
        var selectedSubjectId = SelectedSubject?.Id ?? _subjectService.Settings.LastSubjectId;
        var statisticsFilterId = SelectedStatisticsSubject?.SubjectId;
        var historyFilterId = SelectedHistorySubject?.SubjectId;

        ActiveSubjects.Clear();
        foreach (var subject in _subjectService.ActiveSubjects)
        {
            ActiveSubjects.Add(subject);
        }

        SelectedSubject = ActiveSubjects.FirstOrDefault(subject => subject.Id == selectedSubjectId)
            ?? _subjectService.LastSubject;

        RebuildFilterOptions(StatisticsSubjectOptions);
        RebuildFilterOptions(HistorySubjectOptions);
        SelectedStatisticsSubject = StatisticsSubjectOptions.FirstOrDefault(option => option.SubjectId == statisticsFilterId)
            ?? SubjectFilterOption.All;
        SelectedHistorySubject = HistorySubjectOptions.FirstOrDefault(option => option.SubjectId == historyFilterId)
            ?? SubjectFilterOption.All;
        NotifyCurrentSubjectProperties();
    }

    public void Restore(TimerSnapshot snapshot)
    {
        _engine.Restore(snapshot);
        var restored = ActiveSubjects.FirstOrDefault(subject => subject.Id == _engine.ActiveSubject.Id);
        if (restored is not null)
        {
            _selectedSubject = restored;
        }

        UpdateTimerStateText();
        Tick();
        SaveRecovery();
    }

    public void StopAndSave()
    {
        RunSafely(() =>
        {
            var sessions = _engine.Stop(DateTime.Now);
            if (sessions.Count > 0)
            {
                _repository.AddSessions(sessions);
            }

            _recoveryStore.Clear();
            UpdateTimerStateText();
            RefreshAll();
        }, "保存学习记录失败");
    }

    public void SaveRecovery()
    {
        RunSafely(() =>
        {
            if (Status == TimerStatus.Stopped)
            {
                _recoveryStore.Clear();
            }
            else
            {
                _recoveryStore.Save(_engine.CreateSnapshot(DateTime.Now));
            }
        }, "保存恢复状态失败");
    }

    public void AddHistorySession(StudySession session) => UpdateHistoryRecord(null, session, false);

    public void UpdateHistorySession(StudySession original, StudySession replacement) =>
        UpdateHistoryRecord(original, replacement, false);

    public void DeleteHistorySession(StudySession session) => UpdateHistoryRecord(session, null, true);

    public void RefreshAll()
    {
        RunSafely(() =>
        {
            _currentDate = DateOnly.FromDateTime(DateTime.Now);
            _savedTodayDuration = _repository.GetDuration(_currentDate);
            RefreshStatistics();
            RefreshHistory();
            Tick();
        }, "读取学习记录失败");
    }

    private void ToggleTimer()
    {
        RunSafely(() =>
        {
            var now = DateTime.Now;
            switch (Status)
            {
                case TimerStatus.Stopped:
                    _engine.Start(now, SelectedSubject);
                    break;
                case TimerStatus.Running:
                    _engine.Pause(now);
                    break;
                case TimerStatus.Paused:
                    _engine.Resume(now);
                    break;
            }

            UpdateTimerStateText();
            SaveRecovery();
            Tick();
        }, "计时状态切换失败");
    }

    private void Tick()
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        if (today != _currentDate)
        {
            RefreshAll();
            return;
        }

        TimerText = DurationFormatter.Clock(_engine.GetElapsed(now));
        TodayText = DurationFormatter.Friendly(_savedTodayDuration + GetDurationForDate(_engine.GetSegments(now), today));
        if (Status != TimerStatus.Stopped && ++_heartbeatTicks >= 20)
        {
            _heartbeatTicks = 0;
            SaveRecovery();
        }
    }

    private void UpdateTimerStateText()
    {
        switch (Status)
        {
            case TimerStatus.Running:
                StatusText = "正在专注学习";
                ToggleText = "暂停";
                break;
            case TimerStatus.Paused:
                StatusText = "已暂停，可继续";
                ToggleText = "继续";
                break;
            default:
                StatusText = "准备开始";
                ToggleText = "开始学习";
                break;
        }

        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(CanSelectSubject));
        NotifyCurrentSubjectProperties();
        (StopTimerCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void RefreshStatistics()
    {
        _statisticsReport = _statisticsService.GetReport(
            StatisticsPeriod,
            DateOnly.FromDateTime(StatisticsAnchorDate),
            DateOnly.FromDateTime(DateTime.Today),
            SelectedStatisticsSubject.SubjectId,
            _subjectService.Subjects);

        OnPropertyChanged(nameof(StatisticsPoints));
        OnPropertyChanged(nameof(StatisticsSubjectShares));
        OnPropertyChanged(nameof(StatisticsTitle));
        OnPropertyChanged(nameof(StatisticsTotalText));
        OnPropertyChanged(nameof(StatisticsAverageText));
        OnPropertyChanged(nameof(StatisticsLongestDayText));
        OnPropertyChanged(nameof(StatisticsStreakText));
        OnPropertyChanged(nameof(CanNavigateStatisticsNext));
        OnPropertyChanged(nameof(StatisticsAccessibleSummary));
    }

    private void RefreshHistory()
    {
        RunSafely(() =>
        {
            HistorySessions.Clear();
            var sessions = StudyRecordRepository.FilterSessions(
                _repository.Load(DateOnly.FromDateTime(HistoryDate)).Sessions,
                SelectedHistorySubject.SubjectId).ToArray();
            foreach (var session in sessions)
            {
                HistorySessions.Add(new SessionRow(session, ResolveSubjectColor(session.SubjectId)));
            }

            var total = TimeSpan.FromTicks(sessions.Sum(session => session.Duration.Ticks));
            HistoryTotalText = DurationFormatter.Friendly(total);
            HistoryCountText = $"{sessions.Length} 次";
            OnPropertyChanged(nameof(HistoryTotalText));
            OnPropertyChanged(nameof(HistoryCountText));
            OnPropertyChanged(nameof(HasHistoryRecords));
            OnPropertyChanged(nameof(HasNoHistoryRecords));
        }, "读取当天明细失败");
    }

    private void UpdateHistoryRecord(StudySession? original, StudySession? replacement, bool delete)
    {
        RunSafely(() =>
        {
            var date = DateOnly.FromDateTime(HistoryDate);
            var record = _repository.Load(date);
            var sessions = new List<StudySession>();
            var handled = false;
            foreach (var item in record.Sessions)
            {
                if (!handled && original is not null && item == original)
                {
                    handled = true;
                    if (!delete && replacement is not null)
                    {
                        sessions.Add(replacement);
                    }
                    continue;
                }

                sessions.Add(item);
            }

            if (original is null && replacement is not null)
            {
                sessions.Add(replacement);
            }

            _repository.Save(new DailyStudyRecord(date, sessions.OrderBy(item => item.Start).ToArray()));
            RefreshAll();
        }, delete ? "删除学习记录失败" : original is null ? "新增学习记录失败" : "修改学习记录失败");
    }

    private void RebuildFilterOptions(ObservableCollection<SubjectFilterOption> target)
    {
        target.Clear();
        target.Add(SubjectFilterOption.All);
        foreach (var subject in _subjectService.Subjects)
        {
            target.Add(new SubjectFilterOption(subject.Id, subject.Name + (subject.IsArchived ? "（已归档）" : string.Empty)));
        }
    }

    private string ResolveSubjectColor(Guid id) =>
        _subjectService.Subjects.FirstOrDefault(subject => subject.Id == id)?.Color
        ?? SubjectDefinition.UncategorizedColor;

    private Guid CurrentSubjectId => Status == TimerStatus.Stopped
        ? SelectedSubject.Id
        : _engine.ActiveSubject.Id;

    private void NotifyCurrentSubjectProperties()
    {
        OnPropertyChanged(nameof(CurrentSubjectName));
        OnPropertyChanged(nameof(CurrentSubjectColor));
        OnPropertyChanged(nameof(CurrentSubjectBackground));
    }

    private void OpenDataFolder()
    {
        RunSafely(() =>
        {
            Directory.CreateDirectory(DataRootPath);
            Process.Start(new ProcessStartInfo("explorer.exe", DataRootPath) { UseShellExecute = true });
        }, "打开数据目录失败");
    }

    private void RunSafely(Action action, string context)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke($"{context}：{exception.Message}");
        }
    }

    private static TimeSpan GetDurationForDate(IEnumerable<StudySession> sessions, DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        long ticks = 0;
        foreach (var session in sessions)
        {
            var start = session.Start < dayStart ? dayStart : session.Start;
            var end = session.End > dayEnd ? dayEnd : session.End;
            if (end > start)
            {
                ticks += (end - start).Ticks;
            }
        }

        return TimeSpan.FromTicks(ticks);
    }

    private static string FormatMetric(TimeSpan value)
    {
        var totalHours = (long)Math.Floor(value.TotalHours);
        return totalHours > 0 ? $"{totalHours} 小时 {value.Minutes} 分钟" : $"{value.Minutes} 分钟";
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
