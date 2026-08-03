using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessApp.Common;
using FitnessApp.Models;
using FitnessApp.Services;

namespace FitnessApp.ViewModels;

public partial class RecordActivityViewModel : ViewModelBase, INavigationAware
{
    private const string RequiredMessageFormat = "{0} is required.";
    private const string InvalidNumberMessageFormat =
        "Enter a valid number for {0}.";
    private const string NonFiniteMessageFormat =
        "{0} must be a finite number.";
    private const string RangeMessageFormat =
        "{0} must be between {1} and {2} {3}.";
    private const string WholeNumberMessageFormat =
        "{0} must be a whole number.";
    private const string NullSelectionMessage = "Select an activity.";
    private const string UnsupportedSelectionMessage =
        "Select a supported activity.";
    private const string SafeRecordError =
        "Unable to record your activity right now.";
    private const string SuccessMessage = "Activity recorded successfully.";

    private readonly AuthenticationService _authenticationService;
    private readonly ActivityService _activityService;
    private readonly NavigationService _navigationService;
    private readonly Func<DateTimeOffset> _utcNowProvider;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<ActivityDefinition> _availableActivities =
        Array.Empty<ActivityDefinition>();

    [ObservableProperty]
    private ActivityDefinition? _selectedActivity;

    [ObservableProperty]
    private string _metric1Label = string.Empty;

    [ObservableProperty]
    private string _metric1Unit = string.Empty;

    [ObservableProperty]
    private string _metric1Input = string.Empty;

    [ObservableProperty]
    private string _metric1Guidance = string.Empty;

    [ObservableProperty]
    private string _metric2Label = string.Empty;

    [ObservableProperty]
    private string _metric2Unit = string.Empty;

    [ObservableProperty]
    private string _metric2Input = string.Empty;

    [ObservableProperty]
    private string _metric2Guidance = string.Empty;

    [ObservableProperty]
    private string _metric3Label = string.Empty;

    [ObservableProperty]
    private string _metric3Unit = string.Empty;

    [ObservableProperty]
    private string _metric3Input = string.Empty;

    [ObservableProperty]
    private string _metric3Guidance = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasLoaded;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _estimatedCalories;

    [ObservableProperty]
    private bool _hasResult;

    public RecordActivityViewModel(
        AuthenticationService authenticationService,
        ActivityService activityService,
        NavigationService navigationService,
        Func<DateTimeOffset> utcNowProvider)
    {
        _authenticationService = authenticationService
            ?? throw new ArgumentNullException(nameof(authenticationService));
        _activityService = activityService
            ?? throw new ArgumentNullException(nameof(activityService));
        _navigationService = navigationService
            ?? throw new ArgumentNullException(nameof(navigationService));
        _utcNowProvider = utcNowProvider
            ?? throw new ArgumentNullException(nameof(utcNowProvider));
    }

    public ActivityType? SelectedActivityType => SelectedActivity?.ActivityType;

    // This hook is no-op by default and exists only to coordinate duplicate-command tests.
    internal Func<Task>? BeforeRecordActivityAsync { get; set; }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RecordActivityAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var currentUser = _authenticationService.CurrentUser;
        if (currentUser is null || currentUser.UserId <= 0)
        {
            HandleMissingSession();
            return;
        }

        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        HasResult = false;
        EstimatedCalories = 0;

        if (!TryGetCanonicalDefinition(out var selectedDefinition))
        {
            return;
        }

        if (!TryParseMetric(Metric1Input, selectedDefinition.Metrics[0], out var metric1Value)
            || !TryParseMetric(Metric2Input, selectedDefinition.Metrics[1], out var metric2Value)
            || !TryParseMetric(Metric3Input, selectedDefinition.Metrics[2], out var metric3Value))
        {
            return;
        }

        IsBusy = true;

        try
        {
            if (BeforeRecordActivityAsync is { } beforeRecordActivityAsync)
            {
                await beforeRecordActivityAsync();
            }

            var recordedAtUtc = _utcNowProvider();
            var result = await _activityService.RecordActivityAsync(
                currentUser,
                selectedDefinition.ActivityType,
                metric1Value,
                metric2Value,
                metric3Value,
                recordedAtUtc);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage ?? SafeRecordError;
                return;
            }

            if (result.Value is null || result.Value.ActivityRecordId <= 0)
            {
                ErrorMessage = SafeRecordError;
                return;
            }

            var storedRecord = result.Value;
            ClearMetricInputs();
            EstimatedCalories = storedRecord.CaloriesBurned;
            HasResult = true;
            ErrorMessage = string.Empty;
            StatusMessage = SuccessMessage;
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = SafeRecordError;
            HasResult = false;
            EstimatedCalories = 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        if (IsBusy)
        {
            return;
        }

        ClearMetricInputs();
        HasResult = false;
        EstimatedCalories = 0;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
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
    private void Logout()
    {
        _authenticationService.Logout();
        ClearRecordActivityState();
        _navigationService.Navigate(AppRoute.Login);
    }

    Task INavigationAware.OnNavigatedToAsync()
    {
        if (IsBusy)
        {
            return Task.CompletedTask;
        }

        var currentUser = _authenticationService.CurrentUser;
        if (currentUser is null || currentUser.UserId <= 0)
        {
            HandleMissingSession();
            return Task.CompletedTask;
        }

        Username = currentUser.Username;
        AvailableActivities = _activityService.GetActivityDefinitions();
        SelectedActivity = AvailableActivities.Count > 0
            ? AvailableActivities[0]
            : null;
        ApplySelectedActivityState(SelectedActivity);
        HasLoaded = true;
        return Task.CompletedTask;
    }

    partial void OnSelectedActivityChanged(ActivityDefinition? value)
    {
        OnPropertyChanged(nameof(SelectedActivityType));
        ApplySelectedActivityState(value);
    }

    partial void OnMetric1InputChanged(string value)
    {
        ClearResultAfterInputChange();
    }

    partial void OnMetric2InputChanged(string value)
    {
        ClearResultAfterInputChange();
    }

    partial void OnMetric3InputChanged(string value)
    {
        ClearResultAfterInputChange();
    }

    private void ApplySelectedActivityState(ActivityDefinition? selectedActivity)
    {
        if (selectedActivity is null)
        {
            ClearMetricState();
            return;
        }

        var definitionResult = _activityService.GetActivityDefinition(
            selectedActivity.ActivityType);
        if (!definitionResult.IsSuccess || definitionResult.Value is null)
        {
            ClearMetricState();
            ErrorMessage = UnsupportedSelectionMessage;
            return;
        }

        var definition = definitionResult.Value;
        SetMetricMetadata(definition);
        ClearMetricInputs();
        HasResult = false;
        EstimatedCalories = 0;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    private bool TryGetCanonicalDefinition(out ActivityDefinition definition)
    {
        definition = null!;

        if (SelectedActivity is null)
        {
            ErrorMessage = NullSelectionMessage;
            return false;
        }

        var definitionResult = _activityService.GetActivityDefinition(
            SelectedActivity.ActivityType);
        if (!definitionResult.IsSuccess || definitionResult.Value is null)
        {
            ErrorMessage = UnsupportedSelectionMessage;
            return false;
        }

        definition = definitionResult.Value;
        return true;
    }

    private bool TryParseMetric(
        string input,
        ActivityMetricDefinition metric,
        out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            ErrorMessage = string.Format(
                CultureInfo.InvariantCulture,
                RequiredMessageFormat,
                metric.Label);
            return false;
        }

        var trimmedInput = input.Trim();
        var numberStyles = NumberStyles.AllowLeadingSign
            | NumberStyles.AllowDecimalPoint;
        if (!double.TryParse(
                trimmedInput,
                numberStyles,
                CultureInfo.InvariantCulture,
                out value))
        {
            ErrorMessage = string.Format(
                CultureInfo.InvariantCulture,
                InvalidNumberMessageFormat,
                metric.Label);
            return false;
        }

        if (!double.IsFinite(value))
        {
            ErrorMessage = string.Format(
                CultureInfo.InvariantCulture,
                NonFiniteMessageFormat,
                metric.Label);
            return false;
        }

        if (value < metric.Minimum || value > metric.Maximum)
        {
            ErrorMessage = string.Format(
                CultureInfo.InvariantCulture,
                RangeMessageFormat,
                metric.Label,
                FormatBoundary(metric.Minimum),
                FormatBoundary(metric.Maximum),
                metric.Unit);
            return false;
        }

        if (metric.WholeNumberOnly && value != Math.Truncate(value))
        {
            ErrorMessage = string.Format(
                CultureInfo.InvariantCulture,
                WholeNumberMessageFormat,
                metric.Label);
            return false;
        }

        return true;
    }

    private static string CreateMetricGuidance(ActivityMetricDefinition metric)
    {
        var guidance = string.Format(
            CultureInfo.InvariantCulture,
            "Allowed range: {0} to {1} {2}.",
            FormatBoundary(metric.Minimum),
            FormatBoundary(metric.Maximum),
            metric.Unit);
        return metric.WholeNumberOnly
            ? guidance + " Whole numbers only."
            : guidance;
    }

    private static string FormatBoundary(double value)
    {
        return value.ToString(
            "0.############################",
            CultureInfo.InvariantCulture);
    }

    private void SetMetricMetadata(ActivityDefinition definition)
    {
        var metric1 = definition.Metrics[0];
        var metric2 = definition.Metrics[1];
        var metric3 = definition.Metrics[2];

        Metric1Label = metric1.Label;
        Metric1Unit = metric1.Unit;
        Metric1Guidance = CreateMetricGuidance(metric1);
        Metric2Label = metric2.Label;
        Metric2Unit = metric2.Unit;
        Metric2Guidance = CreateMetricGuidance(metric2);
        Metric3Label = metric3.Label;
        Metric3Unit = metric3.Unit;
        Metric3Guidance = CreateMetricGuidance(metric3);
    }

    private void ClearMetricInputs()
    {
        Metric1Input = string.Empty;
        Metric2Input = string.Empty;
        Metric3Input = string.Empty;
    }

    private void ClearMetricState()
    {
        Metric1Label = string.Empty;
        Metric1Unit = string.Empty;
        Metric1Guidance = string.Empty;
        Metric2Label = string.Empty;
        Metric2Unit = string.Empty;
        Metric2Guidance = string.Empty;
        Metric3Label = string.Empty;
        Metric3Unit = string.Empty;
        Metric3Guidance = string.Empty;
        ClearMetricInputs();
        HasResult = false;
        EstimatedCalories = 0;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    private void ClearResultAfterInputChange()
    {
        if (!HasResult && StatusMessage != SuccessMessage)
        {
            return;
        }

        HasResult = false;
        EstimatedCalories = 0;
        StatusMessage = string.Empty;
    }

    private bool HasValidSession()
    {
        var currentUser = _authenticationService.CurrentUser;
        if (currentUser is not null && currentUser.UserId > 0)
        {
            return true;
        }

        HandleMissingSession();
        return false;
    }

    private void HandleMissingSession()
    {
        _authenticationService.Logout();
        ClearRecordActivityState();
        _navigationService.Navigate(AppRoute.Login);
    }

    private void ClearRecordActivityState()
    {
        Username = string.Empty;
        AvailableActivities = Array.Empty<ActivityDefinition>();
        SelectedActivity = null;
        ClearMetricState();
        HasLoaded = false;
    }
}
