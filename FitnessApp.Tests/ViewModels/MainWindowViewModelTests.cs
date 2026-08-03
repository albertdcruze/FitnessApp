using System;
using System.Collections.Generic;
using FitnessApp.Common;
using FitnessApp.Repositories;
using FitnessApp.Services;
using FitnessApp.ViewModels;
using Xunit;

namespace FitnessApp.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_UsesTheConfiguredLoginViewModel()
    {
        var graph = CreateViewModelGraph();

        var mainWindowViewModel = new MainWindowViewModel(
            graph.NavigationService,
            graph.RouteViewModels);

        Assert.Equal(AppRoute.Login, mainWindowViewModel.CurrentRoute);
        Assert.Same(graph.LoginViewModel, mainWindowViewModel.CurrentViewModel);
    }

    [Fact]
    public void Navigation_SelectsExistingRouteViewModelsAndReusesLogin()
    {
        var graph = CreateViewModelGraph();
        var mainWindowViewModel = new MainWindowViewModel(
            graph.NavigationService,
            graph.RouteViewModels);

        graph.NavigationService.Navigate(AppRoute.Register);
        Assert.Equal(AppRoute.Register, mainWindowViewModel.CurrentRoute);
        Assert.Same(graph.RegisterViewModel, mainWindowViewModel.CurrentViewModel);

        graph.NavigationService.Navigate(AppRoute.Dashboard);
        Assert.Equal(AppRoute.Dashboard, mainWindowViewModel.CurrentRoute);
        Assert.Same(graph.DashboardViewModel, mainWindowViewModel.CurrentViewModel);

        graph.NavigationService.Navigate(AppRoute.Goal);
        Assert.Equal(AppRoute.Goal, mainWindowViewModel.CurrentRoute);
        Assert.Same(graph.GoalViewModel, mainWindowViewModel.CurrentViewModel);

        graph.NavigationService.Navigate(AppRoute.RecordActivity);
        Assert.Equal(AppRoute.RecordActivity, mainWindowViewModel.CurrentRoute);
        Assert.Same(graph.RecordActivityViewModel, mainWindowViewModel.CurrentViewModel);

        graph.NavigationService.Navigate(AppRoute.Login);
        Assert.Equal(AppRoute.Login, mainWindowViewModel.CurrentRoute);
        Assert.Same(graph.LoginViewModel, mainWindowViewModel.CurrentViewModel);
    }

    [Fact]
    public void Navigation_RaisesPropertyChangedForTheRouteAndCurrentViewModel()
    {
        var graph = CreateViewModelGraph();
        var mainWindowViewModel = new MainWindowViewModel(
            graph.NavigationService,
            graph.RouteViewModels);
        var changedProperties = new List<string?>();
        mainWindowViewModel.PropertyChanged += (_, eventArgs) =>
            changedProperties.Add(eventArgs.PropertyName);

        graph.NavigationService.Navigate(AppRoute.Register);

        Assert.Contains(nameof(MainWindowViewModel.CurrentRoute), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.CurrentViewModel), changedProperties);
    }

    [Fact]
    public void SameRouteWithANewStatusMessage_DoesNotReplaceTheCurrentViewModel()
    {
        var graph = CreateViewModelGraph();
        var mainWindowViewModel = new MainWindowViewModel(
            graph.NavigationService,
            graph.RouteViewModels);

        graph.NavigationService.Navigate(AppRoute.Login, "Registration successful.");

        Assert.Equal(AppRoute.Login, mainWindowViewModel.CurrentRoute);
        Assert.Same(graph.LoginViewModel, mainWindowViewModel.CurrentViewModel);
    }

    [Fact]
    public void Constructor_RejectsAMissingRouteMapping()
    {
        var graph = CreateViewModelGraph();
        var incompleteRouteMap = new Dictionary<AppRoute, ViewModelBase>
        {
            [AppRoute.Login] = graph.LoginViewModel,
            [AppRoute.Register] = graph.RegisterViewModel,
            [AppRoute.Dashboard] = graph.DashboardViewModel,
            [AppRoute.Goal] = graph.GoalViewModel,
            [AppRoute.RecordActivity] = null!
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new MainWindowViewModel(graph.NavigationService, incompleteRouteMap));

        Assert.Equal("routeViewModels", exception.ParamName);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var graph = CreateViewModelGraph();

        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(null!, graph.RouteViewModels));
        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(graph.NavigationService, null!));
    }

    private static ViewModelGraph CreateViewModelGraph()
    {
        var authenticationService = new AuthenticationService(
            new UserRepository("Data Source=:memory:"));
        var navigationService = new NavigationService();
        var loginViewModel = new LoginViewModel(
            authenticationService,
            navigationService,
            static () => DateTimeOffset.UtcNow);
        var registerViewModel = new RegisterViewModel(
            authenticationService,
            navigationService);
        var dashboardViewModel = new AuthenticatedRoutePlaceholderViewModel(
            AppRoute.Dashboard,
            "Dashboard",
            authenticationService,
            navigationService);
        var goalViewModel = new AuthenticatedRoutePlaceholderViewModel(
            AppRoute.Goal,
            "Set Daily Goal",
            authenticationService,
            navigationService);
        var recordActivityViewModel = new AuthenticatedRoutePlaceholderViewModel(
            AppRoute.RecordActivity,
            "Record Activity",
            authenticationService,
            navigationService);
        var routeViewModels = new Dictionary<AppRoute, ViewModelBase>
        {
            [AppRoute.Login] = loginViewModel,
            [AppRoute.Register] = registerViewModel,
            [AppRoute.Dashboard] = dashboardViewModel,
            [AppRoute.Goal] = goalViewModel,
            [AppRoute.RecordActivity] = recordActivityViewModel
        };

        return new ViewModelGraph(
            navigationService,
            loginViewModel,
            registerViewModel,
            dashboardViewModel,
            goalViewModel,
            recordActivityViewModel,
            routeViewModels);
    }

    private sealed record ViewModelGraph(
        NavigationService NavigationService,
        LoginViewModel LoginViewModel,
        RegisterViewModel RegisterViewModel,
        AuthenticatedRoutePlaceholderViewModel DashboardViewModel,
        AuthenticatedRoutePlaceholderViewModel GoalViewModel,
        AuthenticatedRoutePlaceholderViewModel RecordActivityViewModel,
        IReadOnlyDictionary<AppRoute, ViewModelBase> RouteViewModels);
}
