using System;
using System.Globalization;
using System.Threading.Tasks;
using FitTrack.Common;
using FitTrack.Models;
using FitTrack.Repositories;
using FitTrack.Services;
using FitTrack.Tests.Data;
using FitTrack.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FitTrack.Tests.ViewModels;

public sealed class DashboardViewModelTests
{
    private const string Password = "FitnessPass1";
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 3, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var authenticationService = new AuthenticationService(
            new UserRepository("Data Source=:memory:"));
        var progressService = new ProgressService(
            new GoalRepository("Data Source=:memory:"),
            new ActivityRepository("Data Source=:memory:"));
        var navigationService = new NavigationService();

        Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(
            null!,
            progressService,
            navigationService,
            static () => FixedUtcNow,
            TimeZoneInfo.Utc));
        Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(
            authenticationService,
            null!,
            navigationService,
            static () => FixedUtcNow,
            TimeZoneInfo.Utc));
        Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(
            authenticationService,
            progressService,
            null!,
            static () => FixedUtcNow,
            TimeZoneInfo.Utc));
        Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(
            authenticationService,
            progressService,
            navigationService,
            null!,
            TimeZoneInfo.Utc));
        Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(
            authenticationService,
            progressService,
            navigationService,
            static () => FixedUtcNow,
            null!));
    }

    [Fact]
    public async Task RefreshAsync_LoadsNoGoalProgressWithoutAnError()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        await fixture.Database.Activities.AddAsync(new ActivityRecord(
            fixture.User.UserId,
            ActivityType.Walking,
            1,
            2,
            3,
            123.456,
            new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)));

        await fixture.RefreshAsync();

        Assert.True(fixture.Dashboard.HasLoaded);
        Assert.False(fixture.Dashboard.HasGoal);
        Assert.Equal(fixture.User.Username, fixture.Dashboard.Username);
        Assert.Equal(123.456, fixture.Dashboard.TotalCalories);
        Assert.Equal(0, fixture.Dashboard.TargetCalories);
        Assert.Equal(0, fixture.Dashboard.RemainingCalories);
        Assert.Equal(0, fixture.Dashboard.ProgressPercentage);
        Assert.Equal(0, fixture.Dashboard.ProgressBarValue);
        Assert.False(fixture.Dashboard.IsGoalAchieved);
        Assert.Equal("No daily calorie goal has been set.", fixture.Dashboard.StatusMessage);
        Assert.Empty(fixture.Dashboard.ErrorMessage);
        Assert.True(fixture.Dashboard.ShowNoGoalPrompt);
        Assert.False(fixture.Dashboard.ShowGoalProgress);
    }

    [Fact]
    public async Task RefreshAsync_LoadsGoalWithNoActivity()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            300,
            FixedUtcNow));

        await fixture.RefreshAsync();

        Assert.True(fixture.Dashboard.HasLoaded);
        Assert.True(fixture.Dashboard.HasGoal);
        Assert.Equal(300, fixture.Dashboard.TargetCalories);
        Assert.Equal(0, fixture.Dashboard.TotalCalories);
        Assert.Equal(300, fixture.Dashboard.RemainingCalories);
        Assert.Equal(0, fixture.Dashboard.ProgressPercentage);
        Assert.Equal(0, fixture.Dashboard.ProgressBarValue);
        Assert.False(fixture.Dashboard.IsGoalAchieved);
        Assert.Equal("Goal not achieved yet.", fixture.Dashboard.StatusMessage);
        Assert.True(fixture.Dashboard.ShowGoalProgress);
        Assert.False(fixture.Dashboard.ShowNoGoalPrompt);
    }

    [Fact]
    public async Task ActivationCopiesNavigationMessageWithoutReplacingProgressStatus()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            300,
            FixedUtcNow));
        fixture.NavigationService.Navigate(
            AppRoute.Dashboard,
            "Daily calorie goal saved.");

        await fixture.RefreshAsync();

        Assert.Equal("Daily calorie goal saved.", fixture.Dashboard.NavigationMessage);
        Assert.Equal("Goal not achieved yet.", fixture.Dashboard.StatusMessage);

        fixture.NavigationService.Navigate(AppRoute.Goal);
        fixture.NavigationService.Navigate(AppRoute.Dashboard);
        await fixture.RefreshAsync();

        Assert.Empty(fixture.Dashboard.NavigationMessage);
        Assert.Equal("Goal not achieved yet.", fixture.Dashboard.StatusMessage);
    }

    [Theory]
    [InlineData(120, 180, 40, 40, false, "Goal not achieved yet.")]
    [InlineData(300, 0, 100, 100, true, "Goal achieved.")]
    [InlineData(450, 0, 150, 100, true, "Goal achieved.")]
    public async Task RefreshAsync_MapsBelowExactAndAboveGoalSummaries(
        double calories,
        double expectedRemaining,
        double expectedPercentage,
        double expectedBarValue,
        bool expectedAchieved,
        string expectedStatus)
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            300,
            FixedUtcNow));
        await fixture.Database.Activities.AddAsync(new ActivityRecord(
            fixture.User.UserId,
            ActivityType.Running,
            1,
            2,
            3,
            calories,
            new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)));

        await fixture.RefreshAsync();

        Assert.Equal(calories, fixture.Dashboard.TotalCalories);
        Assert.Equal(expectedRemaining, fixture.Dashboard.RemainingCalories);
        Assert.Equal(expectedPercentage, fixture.Dashboard.ProgressPercentage);
        Assert.Equal(expectedBarValue, fixture.Dashboard.ProgressBarValue);
        Assert.Equal(expectedAchieved, fixture.Dashboard.IsGoalAchieved);
        Assert.Equal(expectedStatus, fixture.Dashboard.StatusMessage);
    }

    [Fact]
    public async Task RefreshAsync_PreservesStoredCaloriePrecision()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            500,
            FixedUtcNow));
        await fixture.Database.Activities.AddAsync(new ActivityRecord(
            fixture.User.UserId,
            ActivityType.Cycling,
            1,
            2,
            3,
            123.456,
            new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)));

        await fixture.RefreshAsync();

        Assert.Equal(123.456, fixture.Dashboard.TotalCalories);
        Assert.Equal(376.544, fixture.Dashboard.RemainingCalories);
        Assert.Equal(123.456 / 500 * 100, fixture.Dashboard.ProgressPercentage);
    }

    [Fact]
    public async Task RefreshAsync_UsesTheSuppliedUtcClockAndUtcPlusSixThirtyLocalDate()
    {
        await using var fixture = await DashboardFixture.CreateAsync(CreateUtcPlusSixThirty());
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            500,
            FixedUtcNow));
        await fixture.Database.Activities.AddAsync(new ActivityRecord(
            fixture.User.UserId,
            ActivityType.Walking,
            1,
            2,
            3,
            10,
            new DateTimeOffset(2026, 8, 3, 17, 29, 59, TimeSpan.Zero)));
        await fixture.Database.Activities.AddAsync(new ActivityRecord(
            fixture.User.UserId,
            ActivityType.Walking,
            1,
            2,
            3,
            20,
            new DateTimeOffset(2026, 8, 3, 17, 30, 0, TimeSpan.Zero)));
        await fixture.Database.Activities.AddAsync(new ActivityRecord(
            fixture.User.UserId,
            ActivityType.Walking,
            1,
            2,
            3,
            30,
            new DateTimeOffset(2026, 8, 4, 17, 29, 59, TimeSpan.Zero)));
        await fixture.Database.Activities.AddAsync(new ActivityRecord(
            fixture.User.UserId,
            ActivityType.Walking,
            1,
            2,
            3,
            40,
            new DateTimeOffset(2026, 8, 4, 17, 30, 0, TimeSpan.Zero)));

        await fixture.RefreshAsync();

        Assert.Equal(50, fixture.Dashboard.TotalCalories);
    }

    [Fact]
    public async Task RefreshAsync_MissingSessionClearsStateAndReturnsToLogin()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            300,
            FixedUtcNow));
        await fixture.RefreshAsync();
        fixture.AuthenticationService.Logout();

        await fixture.RefreshAsync();

        Assert.Equal(AppRoute.Login, fixture.NavigationService.CurrentRoute);
        Assert.False(fixture.Dashboard.HasLoaded);
        Assert.Empty(fixture.Dashboard.Username);
        Assert.False(fixture.Dashboard.HasGoal);
        Assert.Equal(0, fixture.Dashboard.TotalCalories);
        Assert.Equal(0, fixture.Dashboard.ProgressPercentage);
        Assert.Empty(fixture.Dashboard.StatusMessage);
        Assert.Empty(fixture.Dashboard.ErrorMessage);
        Assert.False(fixture.Dashboard.IsBusy);
    }

    [Fact]
    public async Task RefreshAsync_RepositoryFailureIsSafeAndRetainsPreviousValues()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            300,
            FixedUtcNow));
        await fixture.Database.Activities.AddAsync(new ActivityRecord(
            fixture.User.UserId,
            ActivityType.Walking,
            1,
            2,
            3,
            120,
            new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)));
        await fixture.RefreshAsync();
        var previousTotal = fixture.Dashboard.TotalCalories;
        var previousStatus = fixture.Dashboard.StatusMessage;

        await DropTableAsync(fixture.Database, "ActivityRecords");
        var exception = await Record.ExceptionAsync(() => fixture.RefreshAsync());

        Assert.Null(exception);
        Assert.Equal(AppRoute.Dashboard, fixture.NavigationService.CurrentRoute);
        Assert.Equal("Unable to load your progress right now.", fixture.Dashboard.ErrorMessage);
        Assert.Equal(previousTotal, fixture.Dashboard.TotalCalories);
        Assert.Equal(previousStatus, fixture.Dashboard.StatusMessage);
        Assert.True(fixture.Dashboard.HasLoaded);
        Assert.False(fixture.Dashboard.IsBusy);
        Assert.DoesNotContain("SQL", fixture.Dashboard.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ActivityRecords", fixture.Dashboard.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("fittrack.db", fixture.Dashboard.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_GoalRepositoryFailureIsSafe()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        await DropTableAsync(fixture.Database, "FitnessGoals");

        var exception = await Record.ExceptionAsync(() => fixture.RefreshAsync());

        Assert.Null(exception);
        Assert.Equal(AppRoute.Dashboard, fixture.NavigationService.CurrentRoute);
        Assert.Equal("Unable to load your progress right now.", fixture.Dashboard.ErrorMessage);
        Assert.False(fixture.Dashboard.HasLoaded);
        Assert.False(fixture.Dashboard.IsBusy);
    }

    [Fact]
    public async Task RefreshCommand_PreventsASecondRefreshWhileTheFirstIsLoading()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        fixture.Dashboard.BeforeProgressLoadAsync = async () =>
        {
            invocationCount++;
            entered.SetResult(true);
            await release.Task;
        };

        var firstRefresh = fixture.Dashboard.RefreshCommand.ExecuteAsync(null);
        await entered.Task;
        var secondRefresh = fixture.Dashboard.RefreshCommand.ExecuteAsync(null);
        release.SetResult(true);

        var exception = await Record.ExceptionAsync(async () =>
            await Task.WhenAll(firstRefresh, secondRefresh));

        Assert.Null(exception);
        Assert.Equal(1, invocationCount);
        Assert.True(fixture.Dashboard.HasLoaded);
        Assert.False(fixture.Dashboard.IsBusy);
    }

    [Fact]
    public async Task NavigationCommandsNavigateOnlyForAnActiveSession()
    {
        await using var fixture = await DashboardFixture.CreateAsync();

        fixture.Dashboard.NavigateToGoalCommand.Execute(null);
        Assert.Equal(AppRoute.Goal, fixture.NavigationService.CurrentRoute);

        fixture.NavigationService.Navigate(AppRoute.Dashboard);
        fixture.Dashboard.NavigateToRecordActivityCommand.Execute(null);
        Assert.Equal(AppRoute.RecordActivity, fixture.NavigationService.CurrentRoute);

        fixture.AuthenticationService.Logout();
        fixture.NavigationService.Navigate(AppRoute.Dashboard);
        fixture.Dashboard.NavigateToGoalCommand.Execute(null);

        Assert.Equal(AppRoute.Login, fixture.NavigationService.CurrentRoute);
        Assert.Empty(fixture.Dashboard.Username);
        Assert.False(fixture.Dashboard.HasLoaded);
    }

    [Fact]
    public async Task LogoutCommandClearsStateAndLeavesStoredRowsIntact()
    {
        await using var fixture = await DashboardFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            300,
            FixedUtcNow));
        await fixture.Database.Activities.AddAsync(new ActivityRecord(
            fixture.User.UserId,
            ActivityType.Walking,
            1,
            2,
            3,
            120,
            new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)));
        await fixture.RefreshAsync();

        fixture.Dashboard.LogoutCommand.Execute(null);

        Assert.Null(fixture.AuthenticationService.CurrentUser);
        Assert.Equal(AppRoute.Login, fixture.NavigationService.CurrentRoute);
        Assert.False(fixture.Dashboard.HasLoaded);
        Assert.Empty(fixture.Dashboard.Username);
        Assert.Empty(fixture.Dashboard.NavigationMessage);
        Assert.Equal(1, await CountRowsAsync(fixture.Database, "Users"));
        Assert.Equal(1, await CountRowsAsync(fixture.Database, "FitnessGoals"));
        Assert.Equal(1, await CountRowsAsync(fixture.Database, "ActivityRecords"));
    }

    private static TimeZoneInfo CreateUtcPlusSixThirty()
    {
        return TimeZoneInfo.CreateCustomTimeZone(
            "FitTrack UTC+06:30",
            TimeSpan.FromMinutes(390),
            "FitTrack UTC+06:30",
            "FitTrack UTC+06:30");
    }

    private static async Task DropTableAsync(
        RepositoryTestDatabase database,
        string tableName)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {tableName};";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountRowsAsync(
        RepositoryTestDatabase database,
        string tableName)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture);
    }

    private sealed class DashboardFixture : IAsyncDisposable
    {
        private DashboardFixture(
            RepositoryTestDatabase database,
            AuthenticationService authenticationService,
            NavigationService navigationService,
            DashboardViewModel dashboard,
            User user)
        {
            Database = database;
            AuthenticationService = authenticationService;
            NavigationService = navigationService;
            Dashboard = dashboard;
            User = user;
        }

        public RepositoryTestDatabase Database { get; }

        public AuthenticationService AuthenticationService { get; }

        public NavigationService NavigationService { get; }

        public DashboardViewModel Dashboard { get; }

        public User User { get; }

        public static async Task<DashboardFixture> CreateAsync(
            TimeZoneInfo? timeZone = null)
        {
            var database = await RepositoryTestDatabase.CreateAsync();
            try
            {
                var authenticationService = new AuthenticationService(database.Users);
                var registration = await authenticationService
                    .RegisterAsync("DashboardUser01", Password);
                Assert.True(registration.IsSuccess);
                var login = await authenticationService.LoginAsync(
                    "DashboardUser01",
                    Password,
                    FixedUtcNow);
                Assert.True(login.IsSuccess);
                Assert.NotNull(login.Value);

                var navigationService = new NavigationService();
                var dashboard = new DashboardViewModel(
                    authenticationService,
                    new ProgressService(database.Goals, database.Activities),
                    navigationService,
                    static () => FixedUtcNow,
                    timeZone ?? TimeZoneInfo.Utc);
                navigationService.Navigate(AppRoute.Dashboard);

                return new DashboardFixture(
                    database,
                    authenticationService,
                    navigationService,
                    dashboard,
                    login.Value!);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public Task RefreshAsync()
        {
            return ((INavigationAware)Dashboard).OnNavigatedToAsync();
        }

        public ValueTask DisposeAsync()
        {
            return Database.DisposeAsync();
        }
    }
}
