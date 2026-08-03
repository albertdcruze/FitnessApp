using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessApp.Common;
using FitnessApp.Services;

namespace FitnessApp.ViewModels;

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
            var result = await _progressService
                .GetTodayProgressAsync(currentUser, localDate, _timeZone);

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
    }
}
