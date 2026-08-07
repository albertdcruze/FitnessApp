using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitTrack.Common;
using FitTrack.Models;
using FitTrack.Services;

namespace FitTrack.ViewModels;

public partial class DashboardViewModel : ViewModelBase, INavigationAware
{
    private const string SafeLoadError = "Unable to load your progress right now.";

    private readonly AuthenticationService _authenticationService;
    private readonly ProgressService _progressService;
    private readonly NavigationService _navigationService;
    private readonly Func<DateTimeOffset> _utcNowProvider;
    private readonly TimeZoneInfo _timeZone;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private bool _hasGoal;

    [ObservableProperty]
    private double _targetCalories;

    [ObservableProperty]
    private double _totalCalories;

    [ObservableProperty]
    private double _remainingCalories;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private double _progressBarValue;

    [ObservableProperty]
    private bool _isGoalAchieved;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _navigationMessage = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasLoaded;

    [ObservableProperty]
    private IReadOnlyList<RecentActivityItem> _recentActivities =
        Array.Empty<RecentActivityItem>();

    [ObservableProperty]
    private IReadOnlyList<DailyCaloriePoint> _lastSevenDays =
        Array.Empty<DailyCaloriePoint>();

    [ObservableProperty]
    private int _activitiesThisWeek;

    [ObservableProperty]
    private double _averageCaloriesThisWeek;

    [ObservableProperty]
    private bool _hasRecentActivities;

    public DashboardViewModel(
        AuthenticationService authenticationService,
        ProgressService progressService,
        NavigationService navigationService,
        Func<DateTimeOffset> utcNowProvider,
        TimeZoneInfo timeZone)
    {
        _authenticationService = authenticationService
            ?? throw new ArgumentNullException(nameof(authenticationService));
        _progressService = progressService
            ?? throw new ArgumentNullException(nameof(progressService));
        _navigationService = navigationService
            ?? throw new ArgumentNullException(nameof(navigationService));
        _utcNowProvider = utcNowProvider
            ?? throw new ArgumentNullException(nameof(utcNowProvider));
        _timeZone = timeZone
            ?? throw new ArgumentNullException(nameof(timeZone));
    }

    public bool ShowGoalProgress => HasLoaded && HasGoal;

    public bool ShowNoGoalPrompt => HasLoaded && !HasGoal;

    // This hook is intentionally internal and no-op by default. Tests can use it
    // to coordinate the refresh boundary without changing normal application flow.
    internal Func<Task>? BeforeProgressLoadAsync { get; set; }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var currentUser = _authenticationService.CurrentUser;
        if (currentUser is null || currentUser.UserId <= 0)
        {
            _authenticationService.Logout();
            ClearDashboardState();
            _navigationService.Navigate(AppRoute.Login);
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            Username = currentUser.Username;

            if (BeforeProgressLoadAsync is { } beforeProgressLoadAsync)
            {
                await beforeProgressLoadAsync();
            }

            var nowUtc = _utcNowProvider().ToUniversalTime();
            var localNow = TimeZoneInfo.ConvertTime(nowUtc, _timeZone);
            var localDate = DateOnly.FromDateTime(localNow.DateTime);
            var lastSevenDaysStart = localDate.AddDays(-6);
            var thisWeekStart = localDate.AddDays(-GetDaysSinceMonday(localDate));
            var rangeStartUtc = ConvertLocalStartToUtc(lastSevenDaysStart);
            var rangeEndUtc = ConvertLocalStartToUtc(localDate.AddDays(1));

            var progressTask = _progressService
                .GetTodayProgressAsync(currentUser, localDate, _timeZone);
            var activityRangeTask = _progressService.GetActivitiesAsync(
                currentUser.UserId,
                rangeStartUtc,
                rangeEndUtc);
            var recentActivityTask = _progressService.GetRecentActivitiesAsync(
                currentUser.UserId,
                5);

            await Task.WhenAll(progressTask, activityRangeTask, recentActivityTask);
            var result = await progressTask;

            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = SafeLoadError;
                return;
            }

            var summary = result.Value;
            HasGoal = summary.HasGoal;
            TargetCalories = summary.TargetCalories;
            TotalCalories = summary.TotalCalories;
            RemainingCalories = summary.RemainingCalories;
            ProgressPercentage = summary.ProgressPercentage;
            ProgressBarValue = Math.Clamp(summary.ProgressPercentage, 0, 100);
            IsGoalAchieved = summary.IsGoalAchieved;
            StatusMessage = summary.StatusMessage;

            var activityRange = await activityRangeTask;
            LastSevenDays = CreateDailyCaloriePoints(
                activityRange,
                lastSevenDaysStart,
                localDate);

            var thisWeekActivities = activityRange
                .Where(record => GetLocalDate(record.RecordedAtUtc) >= thisWeekStart)
                .ToArray();
            ActivitiesThisWeek = thisWeekActivities.Length;
            AverageCaloriesThisWeek = thisWeekActivities.Length == 0
                ? 0
                : thisWeekActivities.Average(record => record.CaloriesBurned);

            RecentActivities = (await recentActivityTask)
                .Select(CreateRecentActivityItem)
                .ToArray();
            HasRecentActivities = RecentActivities.Count > 0;
            ErrorMessage = string.Empty;
            HasLoaded = true;
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = SafeLoadError;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NavigateToGoal()
    {
        if (HasValidSession())
        {
            _navigationService.Navigate(AppRoute.Goal);
        }
    }

    [RelayCommand]
    private void NavigateToRecordActivity()
    {
        if (HasValidSession())
        {
            _navigationService.Navigate(AppRoute.RecordActivity);
        }
    }

    [RelayCommand]
    private void Logout()
    {
        _authenticationService.Logout();
        ClearDashboardState();
        _navigationService.Navigate(AppRoute.Login);
    }

    Task INavigationAware.OnNavigatedToAsync()
    {
        NavigationMessage = _navigationService.CurrentStatusMessage ?? string.Empty;
        return RefreshCommand.ExecuteAsync(null);
    }

    partial void OnHasGoalChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowGoalProgress));
        OnPropertyChanged(nameof(ShowNoGoalPrompt));
    }

    partial void OnHasLoadedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowGoalProgress));
        OnPropertyChanged(nameof(ShowNoGoalPrompt));
    }

    private bool HasValidSession()
    {
        var currentUser = _authenticationService.CurrentUser;
        if (currentUser is not null && currentUser.UserId > 0)
        {
            return true;
        }

        _authenticationService.Logout();
        ClearDashboardState();
        _navigationService.Navigate(AppRoute.Login);
        return false;
    }

    private void ClearDashboardState()
    {
        Username = string.Empty;
        HasGoal = false;
        TargetCalories = 0;
        TotalCalories = 0;
        RemainingCalories = 0;
        ProgressPercentage = 0;
        ProgressBarValue = 0;
        IsGoalAchieved = false;
        StatusMessage = string.Empty;
        NavigationMessage = string.Empty;
        ErrorMessage = string.Empty;
        HasLoaded = false;
        RecentActivities = Array.Empty<RecentActivityItem>();
        LastSevenDays = Array.Empty<DailyCaloriePoint>();
        ActivitiesThisWeek = 0;
        AverageCaloriesThisWeek = 0;
        HasRecentActivities = false;
    }

    private static int GetDaysSinceMonday(DateOnly date)
    {
        return ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
    }

    private DateTimeOffset ConvertLocalStartToUtc(DateOnly localDate)
    {
        var localStart = localDate.ToDateTime(
            TimeOnly.MinValue,
            DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, _timeZone));
    }

    private DateOnly GetLocalDate(DateTimeOffset recordedAtUtc)
    {
        var localTime = TimeZoneInfo.ConvertTime(recordedAtUtc, _timeZone);
        return DateOnly.FromDateTime(localTime.DateTime);
    }

    private IReadOnlyList<DailyCaloriePoint> CreateDailyCaloriePoints(
        IReadOnlyList<ActivityRecord> records,
        DateOnly firstDate,
        DateOnly today)
    {
        var totalsByDate = records
            .GroupBy(record => GetLocalDate(record.RecordedAtUtc))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(record => record.CaloriesBurned));

        var totals = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = firstDate.AddDays(offset);
                return (Date: date, Total: totalsByDate.GetValueOrDefault(date));
            })
            .ToArray();
        var maximum = totals.Max(point => point.Total);

        return totals
            .Select(point =>
            {
                var relativeHeight = maximum <= 0
                    ? 0
                    : Math.Max(point.Total / maximum * 112, point.Total > 0 ? 4 : 0);
                var dayLabel = point.Date.ToString("ddd", CultureInfo.CurrentCulture);
                var toolTipText = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0:ddd, MMM d}: {1:0.##} estimated calories",
                    point.Date.ToDateTime(TimeOnly.MinValue),
                    point.Total);

                return new DailyCaloriePoint(
                    point.Date,
                    dayLabel,
                    point.Total,
                    relativeHeight,
                    point.Date == today,
                    toolTipText);
            })
            .ToArray();
    }

    private RecentActivityItem CreateRecentActivityItem(ActivityRecord record)
    {
        var localTime = TimeZoneInfo.ConvertTime(record.RecordedAtUtc, _timeZone);
        return new RecentActivityItem(
            GetActivityName(record.ActivityType),
            record.CaloriesBurned,
            localTime.ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture));
    }

    private static string GetActivityName(ActivityType activityType)
    {
        return activityType switch
        {
            ActivityType.StationaryRowing => "Stationary rowing",
            ActivityType.StrengthTraining => "Strength training",
            _ => activityType.ToString()
        };
    }
}
