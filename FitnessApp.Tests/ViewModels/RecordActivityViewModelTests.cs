using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitnessApp.Calculators;
using FitnessApp.Common;
using FitnessApp.Models;
using FitnessApp.Repositories;
using FitnessApp.Services;
using FitnessApp.Tests.Data;
using FitnessApp.ViewModels;
using Xunit;

namespace FitnessApp.Tests.ViewModels;

public sealed class RecordActivityViewModelTests
{
    private const string Password = "FitnessPass1";
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var authenticationService = new AuthenticationService(
            new UserRepository("Data Source=:memory:"));
        var activityService = new ActivityService(
            new ActivityRepository("Data Source=:memory:"),
            CreateRealCalculators());
        var navigationService = new NavigationService();

        Assert.Throws<ArgumentNullException>(() => new RecordActivityViewModel(
            null!,
            activityService,
            navigationService,
            static () => FixedUtcNow));
        Assert.Throws<ArgumentNullException>(() => new RecordActivityViewModel(
            authenticationService,
            null!,
            navigationService,
            static () => FixedUtcNow));
        Assert.Throws<ArgumentNullException>(() => new RecordActivityViewModel(
            authenticationService,
            activityService,
            null!,
            static () => FixedUtcNow));
        Assert.Throws<ArgumentNullException>(() => new RecordActivityViewModel(
            authenticationService,
            activityService,
            navigationService,
            null!));
    }

    [Fact]
    public async Task ActivationLoadsTheOrderedDefinitionsAndSelectsWalking()
    {
        await using var fixture = await ActivityFixture.CreateAsync();

        Assert.True(fixture.Record.HasLoaded);
        Assert.Equal(fixture.User.Username, fixture.Record.Username);
        Assert.Same(
            fixture.ActivityService.GetActivityDefinitions(),
            fixture.Record.AvailableActivities);
        Assert.Equal(
            new[]
            {
                ActivityType.Walking,
                ActivityType.Swimming,
                ActivityType.Running,
                ActivityType.Cycling,
                ActivityType.StationaryRowing,
                ActivityType.StrengthTraining
            },
            fixture.Record.AvailableActivities.Select(definition => definition.ActivityType));
        Assert.Equal(ActivityType.Walking, fixture.Record.SelectedActivityType);
        Assert.Equal("Steps", fixture.Record.Metric1Label);
        Assert.Equal("steps", fixture.Record.Metric1Unit);
        Assert.Equal(
            "Allowed range: 1 to 100000 steps. Whole numbers only.",
            fixture.Record.Metric1Guidance);
        Assert.Equal("Distance", fixture.Record.Metric2Label);
        Assert.Equal("km", fixture.Record.Metric2Unit);
        Assert.Equal("Allowed range: 0.1 to 100 km.", fixture.Record.Metric2Guidance);
        Assert.Equal("Duration", fixture.Record.Metric3Label);
        Assert.Equal("minutes", fixture.Record.Metric3Unit);
        Assert.Equal("Allowed range: 1 to 720 minutes.", fixture.Record.Metric3Guidance);
        Assert.Empty(fixture.Record.Metric1Input);
        Assert.Empty(fixture.Record.Metric2Input);
        Assert.Empty(fixture.Record.Metric3Input);
        Assert.False(fixture.Record.HasResult);
        Assert.Equal(0, fixture.Record.EstimatedCalories);
        Assert.Empty(fixture.Record.ErrorMessage);
        Assert.Empty(fixture.Record.StatusMessage);
        Assert.Equal(0, await CountActivityRowsAsync(fixture.Database));
    }

    [Theory]
    [InlineData(ActivityType.Walking)]
    [InlineData(ActivityType.Swimming)]
    [InlineData(ActivityType.Running)]
    [InlineData(ActivityType.Cycling)]
    [InlineData(ActivityType.StationaryRowing)]
    [InlineData(ActivityType.StrengthTraining)]
    public async Task SelectingAnActivityCopiesItsCanonicalMetricMetadata(
        ActivityType activityType)
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        var definition = fixture.Record.AvailableActivities
            .Single(activity => activity.ActivityType == activityType);

        fixture.Record.SelectedActivity = definition;

        Assert.Equal(activityType, fixture.Record.SelectedActivityType);
        Assert.Equal(definition.Metrics[0].Label, fixture.Record.Metric1Label);
        Assert.Equal(definition.Metrics[0].Unit, fixture.Record.Metric1Unit);
        Assert.Equal(
            CreateExpectedGuidance(definition.Metrics[0]),
            fixture.Record.Metric1Guidance);
        Assert.Equal(definition.Metrics[1].Label, fixture.Record.Metric2Label);
        Assert.Equal(definition.Metrics[1].Unit, fixture.Record.Metric2Unit);
        Assert.Equal(
            CreateExpectedGuidance(definition.Metrics[1]),
            fixture.Record.Metric2Guidance);
        Assert.Equal(definition.Metrics[2].Label, fixture.Record.Metric3Label);
        Assert.Equal(definition.Metrics[2].Unit, fixture.Record.Metric3Unit);
        Assert.Equal(
            CreateExpectedGuidance(definition.Metrics[2]),
            fixture.Record.Metric3Guidance);
    }

    [Fact]
    public async Task SelectionChangeClearsInputsResultsAndMessages()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.Metric1Input = "5000";
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";
        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);
        Assert.True(fixture.Record.HasResult);

        fixture.Record.Metric1Input = "1";
        fixture.Record.ErrorMessage = "old error";
        fixture.Record.StatusMessage = "old status";
        fixture.Record.SelectedActivity = fixture.Record.AvailableActivities[1];

        Assert.Equal(ActivityType.Swimming, fixture.Record.SelectedActivityType);
        Assert.Empty(fixture.Record.Metric1Input);
        Assert.Empty(fixture.Record.Metric2Input);
        Assert.Empty(fixture.Record.Metric3Input);
        Assert.False(fixture.Record.HasResult);
        Assert.Equal(0, fixture.Record.EstimatedCalories);
        Assert.Empty(fixture.Record.ErrorMessage);
        Assert.Empty(fixture.Record.StatusMessage);
    }

    [Fact]
    public async Task NullSelectionFailsSafelyWithoutRecording()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.SelectedActivity = null;
        fixture.Record.Metric1Input = "5000";
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";

        var exception = await Record.ExceptionAsync(() =>
            fixture.Record.RecordActivityCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.Equal("Select an activity.", fixture.Record.ErrorMessage);
        Assert.Equal(AppRoute.RecordActivity, fixture.NavigationService.CurrentRoute);
        Assert.Equal(0, await CountActivityRowsAsync(fixture.Database));
    }

    [Fact]
    public async Task UnsupportedSelectionFailsWithoutFallingBackToWalking()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.SelectedActivity = new ActivityDefinition(
            (ActivityType)999,
            "Unsupported",
            new[]
            {
                new ActivityMetricDefinition("One", "unit", 1, 2, false),
                new ActivityMetricDefinition("Two", "unit", 1, 2, false),
                new ActivityMetricDefinition("Three", "unit", 1, 2, false)
            });
        fixture.Record.Metric1Input = "1";
        fixture.Record.Metric2Input = "1";
        fixture.Record.Metric3Input = "1";

        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

        Assert.Equal("Select a supported activity.", fixture.Record.ErrorMessage);
        Assert.Equal((ActivityType)999, fixture.Record.SelectedActivityType);
        Assert.False(fixture.Record.HasResult);
        Assert.Equal(0, await CountActivityRowsAsync(fixture.Database));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankMetricInputIsRejected(string input)
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.Metric1Input = input;
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";

        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

        Assert.Equal("Steps is required.", fixture.Record.ErrorMessage);
        Assert.Equal(input, fixture.Record.Metric1Input);
        Assert.False(fixture.Record.IsBusy);
        Assert.Equal(0, await CountActivityRowsAsync(fixture.Database));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1,000")]
    [InlineData("1e3")]
    public async Task InvalidMetricSyntaxIsRejected(string input)
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.Metric1Input = input;
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";

        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

        Assert.Equal("Enter a valid number for Steps.", fixture.Record.ErrorMessage);
        Assert.Equal(input, fixture.Record.Metric1Input);
        Assert.Equal(0, await CountActivityRowsAsync(fixture.Database));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public async Task NonFiniteMetricValuesAreRejected(string input)
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.Metric1Input = input;
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";

        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

        Assert.Equal("Steps must be a finite number.", fixture.Record.ErrorMessage);
        Assert.Equal(0, await CountActivityRowsAsync(fixture.Database));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("100001")]
    public async Task MetricRangeIsValidatedFromTheSelectedDefinition(string input)
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.Metric1Input = input;
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";

        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

        Assert.Equal(
            "Steps must be between 1 and 100000 steps.",
            fixture.Record.ErrorMessage);
        Assert.Equal(input, fixture.Record.Metric1Input);
        Assert.Equal(0, await CountActivityRowsAsync(fixture.Database));
    }

    [Fact]
    public async Task WholeNumberMetricRejectsDecimalInput()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.Metric1Input = "5000.5";
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";

        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

        Assert.Equal("Steps must be a whole number.", fixture.Record.ErrorMessage);
        Assert.Equal(0, await CountActivityRowsAsync(fixture.Database));
    }

    [Fact]
    public async Task SurroundingWhitespaceIsAcceptedAndClearedAfterSuccess()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.Metric1Input = " 5000 ";
        fixture.Record.Metric2Input = " 4 ";
        fixture.Record.Metric3Input = " 60 ";

        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

        Assert.True(fixture.Record.HasResult);
        Assert.Empty(fixture.Record.Metric1Input);
        Assert.Empty(fixture.Record.Metric2Input);
        Assert.Empty(fixture.Record.Metric3Input);
        Assert.Equal(1, await CountActivityRowsAsync(fixture.Database));
    }

    [Theory]
    [MemberData(nameof(SuccessfulActivityCases))]
    public async Task SuccessfulRecordingUsesTheRealActivityService(
        ActivityType activityType,
        double metric1Value,
        double metric2Value,
        double metric3Value,
        double expectedCalories)
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.SelectedActivity = fixture.Record.AvailableActivities
            .Single(definition => definition.ActivityType == activityType);
        fixture.Record.Metric1Input = metric1Value.ToString(
            CultureInfo.InvariantCulture);
        fixture.Record.Metric2Input = metric2Value.ToString(
            CultureInfo.InvariantCulture);
        fixture.Record.Metric3Input = metric3Value.ToString(
            CultureInfo.InvariantCulture);

        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

        Assert.True(fixture.Record.HasResult);
        Assert.Equal(expectedCalories, fixture.Record.EstimatedCalories);
        Assert.Equal("Activity recorded successfully.", fixture.Record.StatusMessage);
        Assert.Empty(fixture.Record.ErrorMessage);
        Assert.Equal(activityType, fixture.Record.SelectedActivityType);
        Assert.Equal(AppRoute.RecordActivity, fixture.NavigationService.CurrentRoute);
        var stored = await ReadFirstActivityAsync(fixture.Database);
        Assert.NotNull(stored);
        Assert.True(stored!.ActivityRecordId > 0);
        Assert.Equal(1, await CountActivityRowsAsync(fixture.Database));
    }

    [Fact]
    public async Task SuccessfulRecordingPassesTheInjectedClockAndPreservesTheUtcInstant()
    {
        var offsetTime = new DateTimeOffset(
            2026,
            8,
            3,
            18,
            30,
            0,
            TimeSpan.FromHours(6.5));
        await using var fixture = await ActivityFixture.CreateAsync(offsetTime);
        fixture.Record.Metric1Input = "5000";
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";

        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

        var stored = await ReadFirstActivityAsync(fixture.Database);
        Assert.NotNull(stored);
        Assert.Equal(offsetTime.ToUniversalTime(), stored!.RecordedAtUtc);
    }

    [Fact]
    public async Task CrossMetricCalculatorFailureIsControlledAndRetainsInputs()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.SelectedActivity = fixture.Record.AvailableActivities
            .Single(definition => definition.ActivityType == ActivityType.Running);
        fixture.Record.Metric1Input = "5";
        fixture.Record.Metric2Input = "30";
        fixture.Record.Metric3Input = "6.61";

        var exception = await Record.ExceptionAsync(() =>
            fixture.Record.RecordActivityCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.Equal("The activity metric values are inconsistent.", fixture.Record.ErrorMessage);
        Assert.Equal("5", fixture.Record.Metric1Input);
        Assert.Equal("30", fixture.Record.Metric2Input);
        Assert.Equal("6.61", fixture.Record.Metric3Input);
        Assert.False(fixture.Record.HasResult);
        Assert.False(fixture.Record.IsBusy);
        Assert.Equal(0, await CountActivityRowsAsync(fixture.Database));
    }

    [Fact]
    public async Task TechnicalFailureIsSafeAndRetainsInputs()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        await RenameActivityTableAsync(fixture.Database);
        fixture.Record.Metric1Input = "5000";
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";

        var exception = await Record.ExceptionAsync(() =>
            fixture.Record.RecordActivityCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.Equal(AppRoute.RecordActivity, fixture.NavigationService.CurrentRoute);
        Assert.Equal(
            "Unable to record your activity right now.",
            fixture.Record.ErrorMessage);
        Assert.Equal("5000", fixture.Record.Metric1Input);
        Assert.Equal("4", fixture.Record.Metric2Input);
        Assert.Equal("60", fixture.Record.Metric3Input);
        Assert.False(fixture.Record.HasResult);
        Assert.False(fixture.Record.IsBusy);
        Assert.DoesNotContain("ActivityRecords", fixture.Record.ErrorMessage);
        Assert.DoesNotContain("fitnessapp.db", fixture.Record.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateCommandExecutionRecordsOnlyOnce()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hookCount = 0;
        fixture.Record.BeforeRecordActivityAsync = async () =>
        {
            Interlocked.Increment(ref hookCount);
            entered.SetResult(true);
            await release.Task;
        };
        fixture.Record.Metric1Input = "5000";
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";

        var firstRecord = fixture.Record.RecordActivityCommand.ExecuteAsync(null);
        await entered.Task;
        var secondRecord = fixture.Record.RecordActivityCommand.ExecuteAsync(null);
        release.SetResult(true);

        var exception = await Record.ExceptionAsync(async () =>
            await Task.WhenAll(firstRecord, secondRecord));

        Assert.Null(exception);
        Assert.Equal(1, hookCount);
        Assert.Equal(1, await CountActivityRowsAsync(fixture.Database));
        Assert.False(fixture.Record.IsBusy);
    }

    [Fact]
    public async Task InputChangeAfterSuccessClearsTheOldResultAndSuccessMessage()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.Metric1Input = "5000";
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";
        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);
        Assert.True(fixture.Record.HasResult);

        fixture.Record.Metric1Input = "4000";

        Assert.False(fixture.Record.HasResult);
        Assert.Equal(0, fixture.Record.EstimatedCalories);
        Assert.Empty(fixture.Record.StatusMessage);
        Assert.Equal("4000", fixture.Record.Metric1Input);
    }

    [Fact]
    public async Task ClearFormPreservesSelectionAndMetadata()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.SelectedActivity = fixture.Record.AvailableActivities[3];
        var selected = fixture.Record.SelectedActivity;
        var label = fixture.Record.Metric1Label;
        fixture.Record.Metric1Input = "20";
        fixture.Record.Metric2Input = "60";
        fixture.Record.Metric3Input = "20";
        fixture.Record.ErrorMessage = "old error";
        fixture.Record.StatusMessage = "old status";

        fixture.Record.ClearFormCommand.Execute(null);

        Assert.Same(selected, fixture.Record.SelectedActivity);
        Assert.Equal(label, fixture.Record.Metric1Label);
        Assert.Empty(fixture.Record.Metric1Input);
        Assert.Empty(fixture.Record.Metric2Input);
        Assert.Empty(fixture.Record.Metric3Input);
        Assert.Empty(fixture.Record.ErrorMessage);
        Assert.Empty(fixture.Record.StatusMessage);
        Assert.False(fixture.Record.HasResult);
    }

    [Fact]
    public async Task ReenteringRecordActivityResetsTheFormToWalking()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.SelectedActivity = fixture.Record.AvailableActivities[2];
        fixture.Record.Metric1Input = "5";
        fixture.Record.Metric2Input = "30";
        fixture.Record.Metric3Input = "6";
        fixture.Record.ErrorMessage = "old error";
        fixture.Record.StatusMessage = "old status";

        await fixture.ActivateAsync();

        Assert.Equal(ActivityType.Walking, fixture.Record.SelectedActivityType);
        Assert.Empty(fixture.Record.Metric1Input);
        Assert.Empty(fixture.Record.Metric2Input);
        Assert.Empty(fixture.Record.Metric3Input);
        Assert.False(fixture.Record.HasResult);
        Assert.Empty(fixture.Record.ErrorMessage);
        Assert.Empty(fixture.Record.StatusMessage);
    }

    [Fact]
    public async Task MissingSessionActivationClearsStateAndDoesNotTouchPersistence()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        await RenameActivityTableAsync(fixture.Database);
        fixture.AuthenticationService.Logout();

        var exception = await Record.ExceptionAsync(fixture.ActivateAsync);

        Assert.Null(exception);
        Assert.Equal(AppRoute.Login, fixture.NavigationService.CurrentRoute);
        Assert.Empty(fixture.Record.Username);
        Assert.Empty(fixture.Record.AvailableActivities);
        Assert.Null(fixture.Record.SelectedActivity);
        Assert.False(fixture.Record.HasLoaded);
    }

    [Fact]
    public async Task MissingSessionRecordClearsStateAndDoesNotAttemptPersistence()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        await RenameActivityTableAsync(fixture.Database);
        fixture.AuthenticationService.Logout();
        fixture.Record.Metric1Input = "not parsed";

        var exception = await Record.ExceptionAsync(() =>
            fixture.Record.RecordActivityCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.Equal(AppRoute.Login, fixture.NavigationService.CurrentRoute);
        Assert.Empty(fixture.Record.Username);
        Assert.Empty(fixture.Record.Metric1Input);
        Assert.False(fixture.Record.HasLoaded);
    }

    [Fact]
    public async Task BackToDashboardDoesNotSaveAndKeepsTheSession()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.Metric1Input = "5000";
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";

        fixture.Record.BackToDashboardCommand.Execute(null);

        Assert.Equal(AppRoute.Dashboard, fixture.NavigationService.CurrentRoute);
        Assert.NotNull(fixture.AuthenticationService.CurrentUser);
        Assert.Equal(0, await CountActivityRowsAsync(fixture.Database));
    }

    [Fact]
    public async Task LogoutClearsStateWithoutDeletingStoredActivity()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        fixture.Record.Metric1Input = "5000";
        fixture.Record.Metric2Input = "4";
        fixture.Record.Metric3Input = "60";
        await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

        fixture.Record.LogoutCommand.Execute(null);

        Assert.Null(fixture.AuthenticationService.CurrentUser);
        Assert.Equal(AppRoute.Login, fixture.NavigationService.CurrentRoute);
        Assert.Empty(fixture.Record.Username);
        Assert.Empty(fixture.Record.AvailableActivities);
        Assert.Null(fixture.Record.SelectedActivity);
        Assert.Empty(fixture.Record.Metric1Label);
        Assert.Empty(fixture.Record.Metric1Input);
        Assert.False(fixture.Record.HasLoaded);
        Assert.False(fixture.Record.HasResult);
        Assert.Equal(1, await CountActivityRowsAsync(fixture.Database));
    }

    [Fact]
    public async Task SelectedActivityTypeRaisesPropertyChanged()
    {
        await using var fixture = await ActivityFixture.CreateAsync();
        var changedProperties = new List<string?>();
        fixture.Record.PropertyChanged += (_, args) =>
            changedProperties.Add(args.PropertyName);

        fixture.Record.SelectedActivity = fixture.Record.AvailableActivities[1];

        Assert.Contains(nameof(RecordActivityViewModel.SelectedActivityType), changedProperties);
        Assert.Equal(ActivityType.Swimming, fixture.Record.SelectedActivityType);
    }

    [Fact]
    public async Task ParsingUsesInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            await using var fixture = await ActivityFixture.CreateAsync();

            fixture.Record.Metric1Input = "5000";
            fixture.Record.Metric2Input = "4.5";
            fixture.Record.Metric3Input = "60";
            await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

            Assert.True(fixture.Record.HasResult);

            fixture.Record.Metric1Input = "5000";
            fixture.Record.Metric2Input = "4,5";
            fixture.Record.Metric3Input = "60";
            await fixture.Record.RecordActivityCommand.ExecuteAsync(null);

            Assert.Equal("Enter a valid number for Distance.", fixture.Record.ErrorMessage);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    public static IEnumerable<object[]> SuccessfulActivityCases()
    {
        yield return new object[] { ActivityType.Walking, 5000d, 4d, 60d, 245d };
        yield return new object[] { ActivityType.Swimming, 80d, 40d, 140d, 373.33d };
        yield return new object[] { ActivityType.Running, 5d, 30d, 6d, 325.5d };
        yield return new object[] { ActivityType.Cycling, 20d, 60d, 20d, 560d };
        yield return new object[] { ActivityType.StationaryRowing, 30d, 150d, 25d, 385d };
        yield return new object[] { ActivityType.StrengthTraining, 45d, 12d, 2d, 262.5d };
    }

    private static string CreateExpectedGuidance(ActivityMetricDefinition metric)
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

    private static IActivityCalculator[] CreateRealCalculators()
    {
        return
        [
            new WalkingCalculator(),
            new SwimmingCalculator(),
            new RunningCalculator(),
            new CyclingCalculator(),
            new StationaryRowingCalculator(),
            new StrengthTrainingCalculator()
        ];
    }

    private static async Task RenameActivityTableAsync(RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "ALTER TABLE ActivityRecords RENAME TO ActivityRecords_Backup;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountActivityRowsAsync(
        RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ActivityRecords;";
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture);
    }

    private static async Task<ActivitySnapshot?> ReadFirstActivityAsync(
        RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ActivityRecordId, UserId, ActivityType,
                   Metric1Value, Metric2Value, Metric3Value,
                   CaloriesBurned, RecordedAtUtc
            FROM ActivityRecords
            LIMIT 1;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ActivitySnapshot(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetDouble(3),
            reader.GetDouble(4),
            reader.GetDouble(5),
            reader.GetDouble(6),
            DateTimeOffset.Parse(
                reader.GetString(7),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));
    }

    private sealed record ActivitySnapshot(
        long ActivityRecordId,
        long UserId,
        string ActivityType,
        double Metric1Value,
        double Metric2Value,
        double Metric3Value,
        double CaloriesBurned,
        DateTimeOffset RecordedAtUtc);

    private sealed class ActivityFixture : IAsyncDisposable
    {
        private ActivityFixture(
            RepositoryTestDatabase database,
            AuthenticationService authenticationService,
            ActivityService activityService,
            NavigationService navigationService,
            RecordActivityViewModel record,
            User user)
        {
            Database = database;
            AuthenticationService = authenticationService;
            ActivityService = activityService;
            NavigationService = navigationService;
            Record = record;
            User = user;
        }

        public RepositoryTestDatabase Database { get; }

        public AuthenticationService AuthenticationService { get; }

        public ActivityService ActivityService { get; }

        public NavigationService NavigationService { get; }

        public RecordActivityViewModel Record { get; }

        public User User { get; }

        public static async Task<ActivityFixture> CreateAsync(
            DateTimeOffset? utcNow = null)
        {
            var database = await RepositoryTestDatabase.CreateAsync();
            try
            {
                var authenticationService = new AuthenticationService(database.Users);
                var registration = await authenticationService.RegisterAsync(
                    "RecordActivityUser01",
                    Password);
                Assert.True(registration.IsSuccess);
                var login = await authenticationService.LoginAsync(
                    "RecordActivityUser01",
                    Password,
                    FixedUtcNow);
                Assert.True(login.IsSuccess);
                Assert.NotNull(login.Value);

                var activityService = new ActivityService(
                    database.Activities,
                    CreateRealCalculators());
                var navigationService = new NavigationService();
                var clock = utcNow ?? FixedUtcNow;
                var record = new RecordActivityViewModel(
                    authenticationService,
                    activityService,
                    navigationService,
                    () => clock);
                navigationService.Navigate(AppRoute.RecordActivity);
                await ((INavigationAware)record).OnNavigatedToAsync();

                return new ActivityFixture(
                    database,
                    authenticationService,
                    activityService,
                    navigationService,
                    record,
                    login.Value!);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public Task ActivateAsync()
        {
            return ((INavigationAware)Record).OnNavigatedToAsync();
        }

        public ValueTask DisposeAsync()
        {
            return Database.DisposeAsync();
        }
    }
}
