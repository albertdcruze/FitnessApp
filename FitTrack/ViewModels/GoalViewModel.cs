using System;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitTrack.Common;
using FitTrack.Services;

namespace FitTrack.ViewModels;

public partial class GoalViewModel : ViewModelBase, INavigationAware
{
    private const string NoGoalMessage = "No daily calorie goal has been set.";
    private const string RequiredInputMessage = "Enter a daily calorie goal.";
    private const string WhitespaceInputMessage =
        "Enter the goal without leading or trailing spaces.";
    private const string InvalidInputMessage =
        "Goal must be a whole number from 1 to 5,000.";
    private const string SafeLoadError = "Unable to load your goal right now.";
    private const string SafeSaveError = "Unable to save your goal right now.";
    private const string SaveSuccessMessage = "Daily calorie goal saved.";

    private readonly AuthenticationService _authenticationService;
    private readonly GoalService _goalService;
    private readonly NavigationService _navigationService;
    private readonly Func<DateTimeOffset> _utcNowProvider;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _goalInput = string.Empty;

    [ObservableProperty]
    private double _existingTargetCalories;

    [ObservableProperty]
    private bool _hasExistingGoal;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasLoaded;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public GoalViewModel(
        AuthenticationService authenticationService,
        GoalService goalService,
        NavigationService navigationService,
        Func<DateTimeOffset> utcNowProvider)
    {
        _authenticationService = authenticationService
            ?? throw new ArgumentNullException(nameof(authenticationService));
        _goalService = goalService
            ?? throw new ArgumentNullException(nameof(goalService));
        _navigationService = navigationService
            ?? throw new ArgumentNullException(nameof(navigationService));
        _utcNowProvider = utcNowProvider
            ?? throw new ArgumentNullException(nameof(utcNowProvider));
    }

    public bool ShowExistingGoal => HasLoaded && HasExistingGoal;

    public bool ShowNoGoalPrompt => HasLoaded && !HasExistingGoal;

    public bool IsPreset300Selected => GoalInput == "300";

    public bool IsPreset500Selected => GoalInput == "500";

    public bool IsPreset750Selected => GoalInput == "750";

    public bool IsPreset1000Selected => GoalInput == "1000";

    // These hooks are no-op by default and exist only to make concurrency tests deterministic.
    internal Func<Task>? BeforeGoalLoadAsync { get; set; }

    internal Func<Task>? BeforeGoalSaveAsync { get; set; }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadGoalAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var currentUser = _authenticationService.CurrentUser;
        if (currentUser is null || currentUser.UserId <= 0)
        {
            _authenticationService.Logout();
            ClearGoalState();
            _navigationService.Navigate(AppRoute.Login);
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        try
        {
            Username = currentUser.Username;

            if (BeforeGoalLoadAsync is { } beforeGoalLoadAsync)
            {
                await beforeGoalLoadAsync();
            }

            var result = await _goalService.GetGoalAsync(currentUser);
            if (!result.IsSuccess)
            {
                ErrorMessage = SafeLoadError;
                return;
            }

            if (result.Value is null)
            {
                HasExistingGoal = false;
                ExistingTargetCalories = 0;
                GoalInput = string.Empty;
                HasLoaded = true;
                StatusMessage = NoGoalMessage;
                ErrorMessage = string.Empty;
                return;
            }

            HasExistingGoal = true;
            ExistingTargetCalories = result.Value.TargetCalories;
            GoalInput = FormatTarget(result.Value.TargetCalories);
            HasLoaded = true;
            StatusMessage = string.Empty;
            ErrorMessage = string.Empty;
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

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SaveGoalAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var currentUser = _authenticationService.CurrentUser;
        if (currentUser is null || currentUser.UserId <= 0)
        {
            _authenticationService.Logout();
            ClearGoalState();
            _navigationService.Navigate(AppRoute.Login);
            return;
        }

        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        if (!TryParseGoalInput(out var targetCalories))
        {
            return;
        }

        IsBusy = true;

        try
        {
            if (BeforeGoalSaveAsync is { } beforeGoalSaveAsync)
            {
                await beforeGoalSaveAsync();
            }

            var updatedAtUtc = _utcNowProvider();
            var result = await _goalService.SaveGoalAsync(
                currentUser,
                targetCalories,
                updatedAtUtc);

            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = result.ErrorMessage ?? SafeSaveError;
                return;
            }

            HasExistingGoal = true;
            ExistingTargetCalories = result.Value.TargetCalories;
            GoalInput = FormatTarget(result.Value.TargetCalories);
            HasLoaded = true;
            ErrorMessage = string.Empty;
            StatusMessage = SaveSuccessMessage;
            _navigationService.Navigate(AppRoute.Dashboard, SaveSuccessMessage);
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = SafeSaveError;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BackToDashboard()
    {
        if (IsBusy)
        {
            return;
        }

        if (HasValidSession())
        {
            _navigationService.Navigate(AppRoute.Dashboard);
        }
    }

    [RelayCommand]
    private void ApplyGoalPreset(string? preset)
    {
        if (IsBusy || string.IsNullOrEmpty(preset))
        {
            return;
        }

        GoalInput = preset;
    }

    [RelayCommand]
    private void Logout()
    {
        _authenticationService.Logout();
        ClearGoalState();
        _navigationService.Navigate(AppRoute.Login);
    }

    Task INavigationAware.OnNavigatedToAsync()
    {
        return LoadGoalCommand.ExecuteAsync(null);
    }

    partial void OnHasExistingGoalChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowExistingGoal));
        OnPropertyChanged(nameof(ShowNoGoalPrompt));
    }

    partial void OnGoalInputChanged(string value)
    {
        OnPropertyChanged(nameof(IsPreset300Selected));
        OnPropertyChanged(nameof(IsPreset500Selected));
        OnPropertyChanged(nameof(IsPreset750Selected));
        OnPropertyChanged(nameof(IsPreset1000Selected));
    }

    partial void OnHasLoadedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowExistingGoal));
        OnPropertyChanged(nameof(ShowNoGoalPrompt));
    }

    private bool TryParseGoalInput(out int targetCalories)
    {
        targetCalories = 0;

        if (string.IsNullOrWhiteSpace(GoalInput))
        {
            ErrorMessage = RequiredInputMessage;
            return false;
        }

        if (GoalInput != GoalInput.Trim())
        {
            ErrorMessage = WhitespaceInputMessage;
            return false;
        }

        if (!int.TryParse(
                GoalInput,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out targetCalories))
        {
            ErrorMessage = InvalidInputMessage;
            return false;
        }

        return true;
    }

    private bool HasValidSession()
    {
        var currentUser = _authenticationService.CurrentUser;
        if (currentUser is not null && currentUser.UserId > 0)
        {
            return true;
        }

        _authenticationService.Logout();
        ClearGoalState();
        _navigationService.Navigate(AppRoute.Login);
        return false;
    }

    private void ClearGoalState()
    {
        Username = string.Empty;
        GoalInput = string.Empty;
        ExistingTargetCalories = 0;
        HasExistingGoal = false;
        HasLoaded = false;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    private static string FormatTarget(double targetCalories)
    {
        return targetCalories.ToString("0", CultureInfo.InvariantCulture);
    }
}
