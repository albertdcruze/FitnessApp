using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using FitTrack.Calculators;
using FitTrack.Common;
using FitTrack.Models;
using FitTrack.Repositories;
using FitTrack.Services;
using FitTrack.Tests.Data;
using FitTrack.ViewModels;
using Xunit;

namespace FitTrack.Tests.ViewModels;

public sealed class GoalDashboardNavigationIntegrationTests
{
    private const string Password = "FitnessPass1";
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FixedGoalClock =
        new(2026, 8, 3, 18, 30, 0, TimeSpan.FromHours(6.5));

    [Fact]
    public async Task SavingGoalNavigatesToDashboardAndTransfersTheSuccessMessage()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticateAsync("GoalFlowUser01");

        graph.NavigationService.Navigate(AppRoute.Goal);
        await graph.MainWindowViewModel.CurrentActivationTask;
        graph.GoalViewModel.GoalInput = "300";

        await graph.GoalViewModel.SaveGoalCommand.ExecuteAsync(null);
        await graph.MainWindowViewModel.CurrentActivationTask;

        Assert.Equal(AppRoute.Dashboard, graph.NavigationService.CurrentRoute);
        Assert.True(graph.DashboardViewModel.HasGoal);
        Assert.Equal(300, graph.DashboardViewModel.TargetCalories);
        Assert.Equal("Daily calorie goal saved.", graph.DashboardViewModel.NavigationMessage);
        Assert.Equal("Goal not achieved yet.", graph.DashboardViewModel.StatusMessage);
        Assert.NotNull(graph.AuthenticationService.CurrentUser);
        Assert.Equal(1, await CountGoalsAsync(graph.Database, graph.AuthenticationService.CurrentUser!.UserId));
    }

    [Fact]
    public async Task SavingAgainUpdatesTheSameGoalRowAndDashboardReloadsIt()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticateAsync("GoalUpdateUser01");

        graph.NavigationService.Navigate(AppRoute.Goal);
        await graph.MainWindowViewModel.CurrentActivationTask;
        graph.GoalViewModel.GoalInput = "1800";
        await graph.GoalViewModel.SaveGoalCommand.ExecuteAsync(null);
        await graph.MainWindowViewModel.CurrentActivationTask;
        var firstGoal = await ReadGoalAsync(
            graph.Database,
            graph.AuthenticationService.CurrentUser!.UserId);
        Assert.NotNull(firstGoal);

        graph.GoalClock.Value = FixedGoalClock.AddHours(1);
        graph.NavigationService.Navigate(AppRoute.Goal);
        await graph.MainWindowViewModel.CurrentActivationTask;
        Assert.Equal("1800", graph.GoalViewModel.GoalInput);
        graph.GoalViewModel.GoalInput = "3200";
        await graph.GoalViewModel.SaveGoalCommand.ExecuteAsync(null);
        await graph.MainWindowViewModel.CurrentActivationTask;

        var secondGoal = await ReadGoalAsync(
            graph.Database,
            graph.AuthenticationService.CurrentUser!.UserId);
        Assert.NotNull(secondGoal);
        Assert.Equal(firstGoal!.GoalId, secondGoal!.GoalId);
        Assert.Equal(3200, secondGoal.TargetCalories);
        Assert.Equal(
            graph.GoalClock.Value.ToUniversalTime(),
            secondGoal.UpdatedAtUtc);
        Assert.Equal(1, await CountGoalsAsync(
            graph.Database,
            graph.AuthenticationService.CurrentUser.UserId));
        Assert.True(graph.DashboardViewModel.HasGoal);
        Assert.Equal(3200, graph.DashboardViewModel.TargetCalories);
    }

    [Fact]
    public async Task ReturningToGoalReloadsTheLatestStoredTarget()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticateAsync("GoalReturnUser01");
        var user = graph.AuthenticationService.CurrentUser!;
        await graph.GoalService.SaveGoalAsync(user, 1500, FixedGoalClock);

        graph.NavigationService.Navigate(AppRoute.Goal);
        await graph.MainWindowViewModel.CurrentActivationTask;
        Assert.Equal(1500, graph.GoalViewModel.ExistingTargetCalories);

        await graph.GoalService.SaveGoalAsync(user, 2600, FixedGoalClock.AddHours(2));
        graph.NavigationService.Navigate(AppRoute.Dashboard);
        await graph.MainWindowViewModel.CurrentActivationTask;
        graph.NavigationService.Navigate(AppRoute.Goal);
        await graph.MainWindowViewModel.CurrentActivationTask;

        Assert.Equal(2600, graph.GoalViewModel.ExistingTargetCalories);
        Assert.Equal("2600", graph.GoalViewModel.GoalInput);
    }

    [Fact]
    public async Task BackToDashboardDoesNotSaveAndClearsOldNavigationMessage()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticateAsync("GoalBackUser01");
        graph.NavigationService.Navigate(AppRoute.Dashboard, "Daily calorie goal saved.");
        await graph.MainWindowViewModel.CurrentActivationTask;
        graph.NavigationService.Navigate(AppRoute.Goal);
        await graph.MainWindowViewModel.CurrentActivationTask;
        graph.GoalViewModel.GoalInput = "1900";

        graph.GoalViewModel.BackToDashboardCommand.Execute(null);
        await graph.MainWindowViewModel.CurrentActivationTask;

        Assert.Equal(AppRoute.Dashboard, graph.NavigationService.CurrentRoute);
        Assert.Empty(graph.DashboardViewModel.NavigationMessage);
        Assert.NotNull(graph.AuthenticationService.CurrentUser);
        Assert.Equal(0, await CountGoalsAsync(
            graph.Database,
            graph.AuthenticationService.CurrentUser!.UserId));
    }

    [Fact]
    public async Task MissingSessionDuringGoalActivationReturnsToLoginSafely()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        graph.NavigationService.Navigate(AppRoute.Goal);
        var activation = graph.MainWindowViewModel.CurrentActivationTask;

        var exception = await Record.ExceptionAsync(() => activation);

        Assert.Null(exception);
        Assert.Equal(AppRoute.Login, graph.NavigationService.CurrentRoute);
        Assert.False(graph.GoalViewModel.HasLoaded);
        Assert.Empty(graph.GoalViewModel.Username);
    }

    [Fact]
    public async Task LogoutClearsGoalStateButLeavesTheStoredGoal()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticateAsync("GoalLogoutUser01");
        var user = graph.AuthenticationService.CurrentUser!;
        await graph.GoalService.SaveGoalAsync(user, 2000, FixedGoalClock);
        graph.NavigationService.Navigate(AppRoute.Goal);
        await graph.MainWindowViewModel.CurrentActivationTask;

        graph.GoalViewModel.LogoutCommand.Execute(null);

        Assert.Null(graph.AuthenticationService.CurrentUser);
        Assert.Equal(AppRoute.Login, graph.NavigationService.CurrentRoute);
        Assert.Empty(graph.GoalViewModel.Username);
        Assert.Empty(graph.GoalViewModel.GoalInput);
        Assert.False(graph.GoalViewModel.HasExistingGoal);
        Assert.False(graph.GoalViewModel.HasLoaded);
        Assert.Equal(1, await CountGoalsAsync(graph.Database, user.UserId));
    }

    [Fact]
    public async Task RecordActivityRouteUsesRecordActivityViewModel()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticateAsync("RecordActivityUser01");

        graph.NavigationService.Navigate(AppRoute.RecordActivity);
        await graph.MainWindowViewModel.CurrentActivationTask;

        Assert.Same(graph.RecordActivityViewModel,
            graph.MainWindowViewModel.CurrentViewModel);
        Assert.True(graph.RecordActivityViewModel.HasLoaded);
        Assert.Equal(6, graph.RecordActivityViewModel.AvailableActivities.Count);
    }

    private static async Task<long> CountGoalsAsync(
        RepositoryTestDatabase database,
        long userId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM FitnessGoals WHERE UserId = $userId;";
        command.Parameters.Add(RepositoryTestDatabase.Parameter("$userId", userId));
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture);
    }

    private static async Task<GoalSnapshot?> ReadGoalAsync(
        RepositoryTestDatabase database,
        long userId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT GoalId, TargetCalories, UpdatedAtUtc
            FROM FitnessGoals
            WHERE UserId = $userId;
            """;
        command.Parameters.Add(RepositoryTestDatabase.Parameter("$userId", userId));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new GoalSnapshot(
            reader.GetInt64(0),
            reader.GetDouble(1),
            DateTimeOffset.Parse(
                reader.GetString(2),
                CultureInfo.InvariantCulture,
                DateStyles));
    }

    private static readonly DateTimeStyles DateStyles =
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private sealed record GoalSnapshot(
        long GoalId,
        double TargetCalories,
        DateTimeOffset UpdatedAtUtc);

    private sealed class ClockValue
    {
        public ClockValue(DateTimeOffset value)
        {
            Value = value;
        }

        public DateTimeOffset Value { get; set; }
    }

    private sealed class CompositionGraph : IAsyncDisposable
    {
        private CompositionGraph(
            RepositoryTestDatabase database,
            AuthenticationService authenticationService,
            GoalService goalService,
            NavigationService navigationService,
            LoginViewModel loginViewModel,
            RegisterViewModel registerViewModel,
            DashboardViewModel dashboardViewModel,
            GoalViewModel goalViewModel,
            RecordActivityViewModel recordActivityViewModel,
            MainWindowViewModel mainWindowViewModel,
            ClockValue goalClock)
        {
            Database = database;
            AuthenticationService = authenticationService;
            GoalService = goalService;
            NavigationService = navigationService;
            LoginViewModel = loginViewModel;
            RegisterViewModel = registerViewModel;
            DashboardViewModel = dashboardViewModel;
            GoalViewModel = goalViewModel;
            RecordActivityViewModel = recordActivityViewModel;
            MainWindowViewModel = mainWindowViewModel;
            GoalClock = goalClock;
        }

        public RepositoryTestDatabase Database { get; }

        public AuthenticationService AuthenticationService { get; }

        public GoalService GoalService { get; }

        public NavigationService NavigationService { get; }

        public LoginViewModel LoginViewModel { get; }

        public RegisterViewModel RegisterViewModel { get; }

        public DashboardViewModel DashboardViewModel { get; }

        public GoalViewModel GoalViewModel { get; }

        public RecordActivityViewModel RecordActivityViewModel { get; }

        public MainWindowViewModel MainWindowViewModel { get; }

        public ClockValue GoalClock { get; }

        public static async Task<CompositionGraph> CreateAsync()
        {
            var database = await RepositoryTestDatabase.CreateAsync();
            try
            {
                var authenticationService = new AuthenticationService(database.Users);
                var goalService = new GoalService(database.Goals);
                var navigationService = new NavigationService();
                var loginViewModel = new LoginViewModel(
                    authenticationService,
                    navigationService,
                    static () => FixedUtcNow);
                var registerViewModel = new RegisterViewModel(
                    authenticationService,
                    navigationService);
                var dashboardViewModel = new DashboardViewModel(
                    authenticationService,
                    new ProgressService(database.Goals, database.Activities),
                    navigationService,
                    static () => FixedUtcNow,
                    TimeZoneInfo.Utc);
                var goalClock = new ClockValue(FixedGoalClock);
                var goalViewModel = new GoalViewModel(
                    authenticationService,
                    goalService,
                    navigationService,
                    () => goalClock.Value);
                IActivityCalculator[] activityCalculators =
                [
                    new WalkingCalculator(),
                    new SwimmingCalculator(),
                    new RunningCalculator(),
                    new CyclingCalculator(),
                    new StationaryRowingCalculator(),
                    new StrengthTrainingCalculator()
                ];
                var activityService = new ActivityService(
                    database.Activities,
                    activityCalculators);
                var recordActivityViewModel = new RecordActivityViewModel(
                    authenticationService,
                    activityService,
                    navigationService,
                    static () => FixedUtcNow);
                IReadOnlyDictionary<AppRoute, ViewModelBase> routeViewModels =
                    new Dictionary<AppRoute, ViewModelBase>
                    {
                        [AppRoute.Login] = loginViewModel,
                        [AppRoute.Register] = registerViewModel,
                        [AppRoute.Dashboard] = dashboardViewModel,
                        [AppRoute.Goal] = goalViewModel,
                        [AppRoute.RecordActivity] = recordActivityViewModel
                    };
                var mainWindowViewModel = new MainWindowViewModel(
                    navigationService,
                    routeViewModels);

                return new CompositionGraph(
                    database,
                    authenticationService,
                    goalService,
                    navigationService,
                    loginViewModel,
                    registerViewModel,
                    dashboardViewModel,
                    goalViewModel,
                    recordActivityViewModel,
                    mainWindowViewModel,
                    goalClock);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async Task AuthenticateAsync(string username)
        {
            var registration = await AuthenticationService.RegisterAsync(username, Password);
            Assert.True(registration.IsSuccess);
            LoginViewModel.Username = username;
            LoginViewModel.Password = Password;
            await LoginViewModel.LoginCommand.ExecuteAsync(null);
            Assert.NotNull(AuthenticationService.CurrentUser);
        }

        public ValueTask DisposeAsync()
        {
            return Database.DisposeAsync();
        }
    }
}
