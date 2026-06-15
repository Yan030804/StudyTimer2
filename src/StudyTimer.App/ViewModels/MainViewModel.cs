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
    private readonly StudyTimerEngine _engine = new();
    private readonly DispatcherTimer _uiTimer;
    private DateOnly _currentDate = DateOnly.FromDateTime(DateTime.Now);
    private TimeSpan _savedTodayDuration;
    private DateTime _historyDate = DateTime.Today;
    private string _timerText = "00:00:00";
    private string _todayText = "0 分钟";
    private string _statusText = "准备开始";
    private string _toggleText = "开始学习";
    private IReadOnlyList<ChartPoint> _sevenDayPoints = Array.Empty<ChartPoint>();
    private IReadOnlyList<ChartPoint> _weeklyPoints = Array.Empty<ChartPoint>();
    private IReadOnlyList<ChartPoint> _monthlyPoints = Array.Empty<ChartPoint>();
    private int _heartbeatTicks;

    public MainViewModel(StudyRecordRepository repository, TimerRecoveryStore recoveryStore)
    {
        _repository = repository;
        _recoveryStore = recoveryStore;
        _statisticsService = new StatisticsService(repository);

        ToggleTimerCommand = new RelayCommand(_ => ToggleTimer());
        StopTimerCommand = new RelayCommand(_ => StopAndSave(), _ => Status != TimerStatus.Stopped);
        OpenDataFolderCommand = new RelayCommand(_ => OpenDataFolder());
        CompactCommand = new RelayCommand(_ => CompactRequested?.Invoke());
        RefreshCommand = new RelayCommand(_ => RefreshAll());

        _uiTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _uiTimer.Tick += (_, _) => Tick();
        _uiTimer.Start();

        RefreshAll();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? ErrorOccurred;
    public event Action? CompactRequested;

    public ObservableCollection<SessionRow> HistorySessions { get; } = [];
    public ICommand ToggleTimerCommand { get; }
    public ICommand StopTimerCommand { get; }
    public ICommand OpenDataFolderCommand { get; }
    public ICommand CompactCommand { get; }
    public ICommand RefreshCommand { get; }
    public TimerStatus Status => _engine.Status;
    public string DataRootPath => _repository.RootPath;

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

    public DateTime HistoryDate
    {
        get => _historyDate;
        set
        {
            if (SetField(ref _historyDate, value))
            {
                RefreshHistory();
            }
        }
    }

    public IReadOnlyList<ChartPoint> SevenDayPoints
    {
        get => _sevenDayPoints;
        private set => SetField(ref _sevenDayPoints, value);
    }

    public IReadOnlyList<ChartPoint> WeeklyPoints
    {
        get => _weeklyPoints;
        private set => SetField(ref _weeklyPoints, value);
    }

    public IReadOnlyList<ChartPoint> MonthlyPoints
    {
        get => _monthlyPoints;
        private set => SetField(ref _monthlyPoints, value);
    }

    public string SevenDaySummary => BuildSummary(SevenDayPoints);
    public string WeeklySummary => BuildSummary(WeeklyPoints);
    public string MonthlySummary => BuildSummary(MonthlyPoints);

    public void Restore(TimerSnapshot snapshot)
    {
        _engine.Restore(snapshot);
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

    public void AddHistorySession(StudySession session)
    {
        RunSafely(() =>
        {
            var date = DateOnly.FromDateTime(HistoryDate);
            var record = _repository.Load(date);
            _repository.Save(new DailyStudyRecord(date,
                record.Sessions.Append(session).OrderBy(item => item.Start).ToArray()));
            RefreshAll();
        }, "新增学习记录失败");
    }

    public void UpdateHistorySession(StudySession original, StudySession replacement)
    {
        RunSafely(() =>
        {
            var date = DateOnly.FromDateTime(HistoryDate);
            var record = _repository.Load(date);
            var replaced = false;
            var sessions = record.Sessions.Select(item =>
                {
                    if (!replaced && item == original)
                    {
                        replaced = true;
                        return replacement;
                    }

                    return item;
                })
                .OrderBy(item => item.Start).ToArray();
            _repository.Save(new DailyStudyRecord(date, sessions));
            RefreshAll();
        }, "修改学习记录失败");
    }

    public void DeleteHistorySession(StudySession session)
    {
        RunSafely(() =>
        {
            var date = DateOnly.FromDateTime(HistoryDate);
            var record = _repository.Load(date);
            var removed = false;
            var sessions = new List<StudySession>();
            foreach (var item in record.Sessions)
            {
                if (!removed && item == session)
                {
                    removed = true;
                    continue;
                }

                sessions.Add(item);
            }

            _repository.Save(new DailyStudyRecord(date, sessions));
            RefreshAll();
        }, "删除学习记录失败");
    }

    public void RefreshAll()
    {
        RunSafely(() =>
        {
            _currentDate = DateOnly.FromDateTime(DateTime.Now);
            _savedTodayDuration = _repository.GetDuration(_currentDate);
            SevenDayPoints = _statisticsService.GetLastSevenDays(_currentDate);
            WeeklyPoints = _statisticsService.GetWeeksOfMonth(_currentDate.Year, _currentDate.Month);
            MonthlyPoints = _statisticsService.GetMonthsOfYear(_currentDate.Year);
            OnPropertyChanged(nameof(SevenDaySummary));
            OnPropertyChanged(nameof(WeeklySummary));
            OnPropertyChanged(nameof(MonthlySummary));
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
                    _engine.Start(now);
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
        var currentToday = GetDurationForDate(_engine.GetSegments(now), today);
        TodayText = DurationFormatter.Friendly(_savedTodayDuration + currentToday);

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
        (StopTimerCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void RefreshHistory()
    {
        RunSafely(() =>
        {
            HistorySessions.Clear();
            var record = _repository.Load(DateOnly.FromDateTime(HistoryDate));
            foreach (var session in record.Sessions)
            {
                HistorySessions.Add(new SessionRow(session));
            }
        }, "读取当天明细失败");
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

    private static string BuildSummary(IEnumerable<ChartPoint> points) => string.Join("；",
        points.Select(point => $"{point.Label.Replace("\n", " ")} {DurationFormatter.Friendly(point.Duration)}"));

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
