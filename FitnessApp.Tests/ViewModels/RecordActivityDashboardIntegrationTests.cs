using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

public sealed class RecordActivityDashboardIntegrationTests
{
    private const string Password = "FitnessPass1";
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordingActivityAndReturningToDashboardRefreshesGoalProgress()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticateAsync("RecordDashboardUser01");
        var user = graph.AuthenticationService.CurrentUser!;
        var goalResult = await graph.GoalService.SaveGoalAsync(
            user,
            200,
            FixedUtcNow);
        Assert.True(goalResult.IsSuccess);

        graph.NavigationService.Navigate(AppRoute.RecordActivity);
        await graph.MainWindowViewModel.CurrentActivationTask;
        Assert.Same(
            graph.RecordActivityViewModel,
            graph.MainWindowViewModel.CurrentViewModel);

        graph.RecordActivityViewModel.Metric1Input = "5000";
        graph.RecordActivityViewModel.Metric2Input = "4";
        graph.RecordActivityViewModel.Metric3Input = "60";
        await graph.RecordActivityViewModel.RecordActivityCommand.ExecuteAsync(null);

        Assert.Equal(AppRoute.RecordActivity, graph.NavigationService.CurrentRoute);
        Assert.True(graph.RecordActivityViewModel.HasResult);
        Assert.Equal(245, graph.RecordActivityViewModel.EstimatedCalories);
        Assert.Equal("Activity recorded successfully.",
            graph.RecordActivityViewModel.StatusMessage);
        Assert.Equal(1, await CountActivityRowsAsync(graph.Database, user.UserId));

        graph.RecordActivityViewModel.BackToDashboardCommand.Execute(null);
        await graph.MainWindowViewModel.CurrentActivationTask;

        Assert.Equal(AppRoute.Dashboard, graph.NavigationService.CurrentRoute);
        Assert.Same(
            graph.DashboardViewModel,
            graph.MainWindowViewModel.CurrentViewModel);
        Assert.Same(user, graph.AuthenticationService.CurrentUser);
        Assert.True(graph.DashboardViewModel.HasGoal);
        Assert.Equal(200, graph.DashboardViewModel.TargetCalories);
        Assert.Equal(245, graph.DashboardViewModel.TotalCalories);
        Assert.True(graph.DashboardViewModel.IsGoalAchieved);
    }

    [Fact]
    public async Task ReturningToRecordActivityResetsTheFormWithoutCreatingAnotherRow()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticateAsync("RecordResetUser01");

        graph.NavigationService.Navigate(AppRoute.RecordActivity);
        await graph.MainWindowViewModel.CurrentActivationTask;
        graph.RecordActivityViewModel.SelectedActivity = graph.RecordActivityViewModel
            .AvailableActivities
            .Single(definition => definition.ActivityType == ActivityType.Cycling);
        graph.RecordActivityViewModel.Metric1Input = "20";
        graph.RecordActivityViewModel.Metric2Input = "60";
        graph.RecordActivityViewModel.Metric3Input = "20";
        await graph.RecordActivityViewModel.RecordActivityCommand.ExecuteAsync(null);

        Assert.True(graph.RecordActivityViewModel.HasResult);
        Assert.Equal(1, await CountActivityRowsAsync(
            graph.Database,
            graph.AuthenticationService.CurrentUser!.UserId));

        graph.RecordActivityViewModel.BackToDashboardCommand.Execute(null);
        await graph.MainWindowViewModel.CurrentActivationTask;
        graph.NavigationService.Navigate(AppRoute.RecordActivity);
        await graph.MainWindowViewModel.CurrentActivationTask;

        Assert.Same(
            graph.RecordActivityViewModel,
            graph.MainWindowViewModel.CurrentViewModel);
        Assert.Equal(ActivityType.Walking,
            graph.RecordActivityViewModel.SelectedActivityType);
        Assert.Empty(graph.RecordActivityViewModel.Metric1Input);
        Assert.Empty(graph.RecordActivityViewModel.Metric2Input);
        Assert.Empty(graph.RecordActivityViewModel.Metric3Input);
        Assert.False(graph.RecordActivityViewModel.HasResult);
        Assert.Empty(graph.RecordActivityViewModel.ErrorMessage);
        Assert.Empty(graph.RecordActivityViewModel.StatusMessage);
        Assert.Equal(1, await CountActivityRowsAsync(
            graph.Database,
            graph.AuthenticationService.CurrentUser!.UserId));
    }

    [Fact]
    public async Task LoggingOutFromRecordActivityReturnsToLoginAndPreservesTheRow()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticateAsync("RecordLogoutUser01");
        var user = graph.AuthenticationService.CurrentUser!;

        graph.NavigationService.Navigate(AppRoute.RecordActivity);
        await graph.MainWindowViewModel.CurrentActivationTask;
        graph.RecordActivityViewModel.Metric1Input = "5000";
        graph.RecordActivityViewModel.Metric2Input = "4";
        graph.RecordActivityViewModel.Metric3Input = "60";
        await graph.RecordActivityViewModel.RecordActivityCommand.ExecuteAsync(null);

        graph.RecordActivityViewModel.LogoutCommand.Execute(null);

        Assert.Null(graph.AuthenticationService.CurrentUser);
        Assert.Equal(AppRoute.Login, graph.NavigationService.CurrentRoute);
        Assert.Same(
            graph.LoginViewModel,
            graph.MainWindowViewModel.CurrentViewModel);
        Assert.Empty(graph.RecordActivityViewModel.Username);
        Assert.Empty(graph.RecordActivityViewModel.AvailableActivities);
        Assert.Null(graph.RecordActivityViewModel.SelectedActivity);
        Assert.False(graph.RecordActivityViewModel.HasLoaded);
        Assert.Equal(1, await CountActivityRowsAsync(graph.Database, user.UserId));
    }

    private static async Task<long> CountActivityRowsAsync(
        RepositoryTestDatabase database,
        long userId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM ActivityRecords WHERE UserId = $userId;";
        command.Parameters.Add(RepositoryTestDatabase.Parameter("$userId", userId));
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture);
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
            MainWindowViewModel mainWindowViewModel)
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

        public static async Task<CompositionGraph> CreateAsync()
        {
            var database = await RepositoryTestDatabase.CreateAsync();
            try
            {
                var authenticationService = new AuthenticationService(database.Users);
                var goalService = new GoalService(database.Goals);
                var progressService = new ProgressService(
                    database.Goals,
                    database.Activities);
                var activityService = new ActivityService(
                    database.Activities,
                    CreateCalculators());
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
                    progressService,
                    navigationService,
                    static () => FixedUtcNow,
                    TimeZoneInfo.Utc);
                var goalViewModel = new GoalViewModel(
                    authenticationService,
                    goalService,
                    navigationService,
                    static () => FixedUtcNow);
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
                    mainWindowViewModel);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async Task AuthenticateAsync(string username)
        {
            var registration = await AuthenticationService.RegisterAsync(
                username,
                Password);
            Assert.True(registration.IsSuccess);

            var login = await AuthenticationService.LoginAsync(
                username,
                Password,
                FixedUtcNow);
            Assert.True(login.IsSuccess);
            Assert.NotNull(AuthenticationService.CurrentUser);
        }

        public ValueTask DisposeAsync()
        {
            return Database.DisposeAsync();
        }

        private static IActivityCalculator[] CreateCalculators()
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
    }
}
