using System;
using System.Collections.Generic;
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

public sealed class DashboardNavigationIntegrationTests
{
    private const string Password = "FitnessPass1";
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 3, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoginActivatesTheRealDashboardAndLoadsTheSharedSession()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        var registration = await graph.AuthenticationService
            .RegisterAsync("IntegratedUser01", Password);
        graph.LoginViewModel.Username = "IntegratedUser01";
        graph.LoginViewModel.Password = Password;

        await graph.LoginViewModel.LoginCommand.ExecuteAsync(null);
        await graph.MainWindowViewModel.CurrentActivationTask;

        Assert.True(registration.IsSuccess);
        Assert.Equal(AppRoute.Dashboard, graph.NavigationService.CurrentRoute);
        Assert.Same(graph.DashboardViewModel, graph.MainWindowViewModel.CurrentViewModel);
        Assert.True(graph.DashboardViewModel.HasLoaded);
        Assert.Equal("IntegratedUser01", graph.DashboardViewModel.Username);
        Assert.Equal(
            registration.Value!.UserId,
            graph.AuthenticationService.CurrentUser!.UserId);
    }

    [Fact]
    public async Task ReturningToDashboardReusesTheViewModelAndRefreshesProgress()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticationService.RegisterAsync("ReturnUser01", Password);
        var login = await graph.AuthenticationService.LoginAsync(
            "ReturnUser01",
            Password,
            FixedUtcNow);
        Assert.True(login.IsSuccess);

        graph.NavigationService.Navigate(AppRoute.Dashboard);
        await graph.MainWindowViewModel.CurrentActivationTask;
        Assert.Equal(0, graph.DashboardViewModel.TotalCalories);

        await graph.Database.Activities.AddAsync(new ActivityRecord(
            graph.AuthenticationService.CurrentUser!.UserId,
            ActivityType.Walking,
            1,
            2,
            3,
            123.456,
            new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)));
        graph.NavigationService.Navigate(AppRoute.Goal);
        graph.NavigationService.Navigate(AppRoute.Dashboard);
        await graph.MainWindowViewModel.CurrentActivationTask;

        Assert.Same(graph.DashboardViewModel, graph.MainWindowViewModel.CurrentViewModel);
        Assert.Equal(123.456, graph.DashboardViewModel.TotalCalories);
    }

    [Fact]
    public async Task RecordActivityRouteUsesRecordActivityViewModel()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticationService.RegisterAsync("RecordActivityUser01", Password);
        var login = await graph.AuthenticationService.LoginAsync(
            "RecordActivityUser01",
            Password,
            FixedUtcNow);
        Assert.True(login.IsSuccess);

        graph.NavigationService.Navigate(AppRoute.Goal);
        await graph.MainWindowViewModel.CurrentActivationTask;
        Assert.Same(graph.GoalViewModel,
            graph.MainWindowViewModel.CurrentViewModel);
        graph.NavigationService.Navigate(AppRoute.RecordActivity);
        await graph.MainWindowViewModel.CurrentActivationTask;
        Assert.Same(graph.RecordActivityViewModel,
            graph.MainWindowViewModel.CurrentViewModel);
        graph.NavigationService.Navigate(AppRoute.Dashboard);
        await graph.MainWindowViewModel.CurrentActivationTask;
        Assert.Same(graph.DashboardViewModel, graph.MainWindowViewModel.CurrentViewModel);
    }

    [Fact]
    public async Task DashboardLogoutClearsTheSharedSessionAndReturnsToLogin()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticationService.RegisterAsync("IntegratedLogout01", Password);
        var login = await graph.AuthenticationService.LoginAsync(
            "IntegratedLogout01",
            Password,
            FixedUtcNow);
        Assert.True(login.IsSuccess);
        graph.NavigationService.Navigate(AppRoute.Dashboard);
        await graph.MainWindowViewModel.CurrentActivationTask;

        graph.DashboardViewModel.LogoutCommand.Execute(null);

        Assert.Null(graph.AuthenticationService.CurrentUser);
        Assert.Equal(AppRoute.Login, graph.NavigationService.CurrentRoute);
        Assert.Same(graph.LoginViewModel, graph.MainWindowViewModel.CurrentViewModel);
        Assert.False(graph.DashboardViewModel.HasLoaded);
    }

    [Fact]
    public async Task ReturningToDashboardWhileItIsLoadingDoesNotStartASecondLoad()
    {
        await using var graph = await CompositionGraph.CreateAsync();
        await graph.AuthenticationService.RegisterAsync("IntegratedBusy01", Password);
        var login = await graph.AuthenticationService.LoginAsync(
            "IntegratedBusy01",
            Password,
            FixedUtcNow);
        Assert.True(login.IsSuccess);

        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCount = 0;
        graph.DashboardViewModel.BeforeProgressLoadAsync = async () =>
        {
            Interlocked.Increment(ref loadCount);
            entered.SetResult(true);
            await release.Task;
        };

        graph.NavigationService.Navigate(AppRoute.Dashboard);
        var firstActivation = graph.MainWindowViewModel.CurrentActivationTask;
        await entered.Task;

        graph.NavigationService.Navigate(AppRoute.Goal);
        graph.NavigationService.Navigate(AppRoute.Dashboard);
        var secondActivation = graph.MainWindowViewModel.CurrentActivationTask;
        release.SetResult(true);

        await firstActivation;
        await secondActivation;

        Assert.Equal(1, loadCount);
        Assert.True(graph.DashboardViewModel.HasLoaded);
        Assert.False(graph.DashboardViewModel.IsBusy);
    }

    private sealed class CompositionGraph : IAsyncDisposable
    {
        private CompositionGraph(
            RepositoryTestDatabase database,
            AuthenticationService authenticationService,
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
                var goalService = new GoalService(database.Goals);
                var goalViewModel = new GoalViewModel(
                    authenticationService,
                    goalService,
                    navigationService,
                    static () => FixedUtcNow);
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

        public ValueTask DisposeAsync()
        {
            return Database.DisposeAsync();
        }
    }
}
