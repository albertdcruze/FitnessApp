using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitnessApp.Calculators;
using FitnessApp.Common;
using FitnessApp.Repositories;
using FitnessApp.Services;
using FitnessApp.Tests.Data;
using FitnessApp.ViewModels;
using Xunit;

namespace FitnessApp.Tests.ViewModels;

public sealed class AuthenticationNavigationIntegrationTests
{
    private const string Password = "FitnessPass1";
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SuccessfulLogin_UsesTheSharedSessionAndDisplaysTheDashboardRoute()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var graph = CreateGraph(database);
        var registration = await graph.AuthenticationService.RegisterAsync("SessionUser01", Password);
        graph.LoginViewModel.Username = "SessionUser01";
        graph.LoginViewModel.Password = Password;

        await graph.LoginViewModel.LoginCommand.ExecuteAsync(null);
        await graph.MainWindowViewModel.CurrentActivationTask;

        Assert.True(registration.IsSuccess);
        Assert.Equal(AppRoute.Dashboard, graph.NavigationService.CurrentRoute);
        Assert.Same(graph.DashboardViewModel, graph.MainWindowViewModel.CurrentViewModel);
        Assert.NotNull(graph.AuthenticationService.CurrentUser);
        Assert.Equal(
            registration.Value!.UserId,
            graph.AuthenticationService.CurrentUser!.UserId);
    }

    [Fact]
    public async Task Logout_ReturnsToTheOriginalLoginViewModelAndClearsTheSharedSession()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var graph = CreateGraph(database);
        await graph.AuthenticationService.RegisterAsync("LogoutUser01", Password);
        graph.LoginViewModel.Username = "LogoutUser01";
        graph.LoginViewModel.Password = Password;
        await graph.LoginViewModel.LoginCommand.ExecuteAsync(null);
        await graph.MainWindowViewModel.CurrentActivationTask;

        graph.DashboardViewModel.LogoutCommand.Execute(null);

        Assert.Null(graph.AuthenticationService.CurrentUser);
        Assert.Equal(AppRoute.Login, graph.NavigationService.CurrentRoute);
        Assert.Same(graph.LoginViewModel, graph.MainWindowViewModel.CurrentViewModel);
    }

    [Fact]
    public async Task SuccessfulRegistration_ReturnsToLoginWithoutCreatingASession()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var graph = CreateGraph(database);
        graph.NavigationService.Navigate(AppRoute.Register);
        graph.RegisterViewModel.Username = "RegistrationUser01";
        graph.RegisterViewModel.Password = Password;
        graph.RegisterViewModel.ConfirmPassword = Password;

        await graph.RegisterViewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(AppRoute.Login, graph.NavigationService.CurrentRoute);
        Assert.Same(graph.LoginViewModel, graph.MainWindowViewModel.CurrentViewModel);
        Assert.Equal(
            "Registration successful. You can now sign in.",
            graph.LoginViewModel.StatusMessage);
        Assert.Null(graph.AuthenticationService.CurrentUser);
    }

    [Theory]
    [InlineData(AppRoute.Login)]
    [InlineData(AppRoute.Register)]
    [InlineData((AppRoute)999)]
    public void RouteTestViewModel_RejectsNonAuthenticatedRoutes(AppRoute route)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RouteTestViewModel(route, "Test route"));
    }

    [Theory]
    [InlineData(AppRoute.Dashboard, "Dashboard")]
    [InlineData(AppRoute.Goal, "Set Daily Goal")]
    [InlineData(AppRoute.RecordActivity, "Record Activity")]
    public void RouteTestViewModel_ExposesItsConfiguredRouteAndTitle(
        AppRoute route,
        string title)
    {
        var viewModel = new RouteTestViewModel(route, title);

        Assert.Equal(route, viewModel.Route);
        Assert.Equal(title, viewModel.Title);
    }

    private static CompositionGraph CreateGraph(RepositoryTestDatabase database)
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
        var goalViewModel = new GoalViewModel(
            authenticationService,
            new GoalService(database.Goals),
            navigationService,
            static () => FixedUtcNow);
        var activityService = new ActivityService(
            database.Activities,
            CreateCalculators());
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
            authenticationService,
            navigationService,
            loginViewModel,
            registerViewModel,
            dashboardViewModel,
            goalViewModel,
            recordActivityViewModel,
            mainWindowViewModel);
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

    private sealed record CompositionGraph(
        AuthenticationService AuthenticationService,
        NavigationService NavigationService,
        LoginViewModel LoginViewModel,
        RegisterViewModel RegisterViewModel,
        DashboardViewModel DashboardViewModel,
        GoalViewModel GoalViewModel,
        RecordActivityViewModel RecordActivityViewModel,
        MainWindowViewModel MainWindowViewModel);
}
