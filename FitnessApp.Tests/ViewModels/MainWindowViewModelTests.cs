using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitnessApp.Common;
using FitnessApp.Repositories;
using FitnessApp.Services;
using FitnessApp.Tests.Data;
using FitnessApp.ViewModels;
using Xunit;

namespace FitnessApp.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_UsesTheConfiguredLoginViewModel()
    {
        using var graph = CreateViewModelGraph();

        var mainWindowViewModel = new MainWindowViewModel(
            graph.NavigationService,
            graph.RouteViewModels);

        Assert.Equal(AppRoute.Login, mainWindowViewModel.CurrentRoute);
        Assert.Same(graph.LoginViewModel, mainWindowViewModel.CurrentViewModel);
    }

    [Fact]
    public void Navigation_SelectsExistingRouteViewModelsAndReusesLogin()
    {
        using var graph = CreateViewModelGraph();
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
        using var graph = CreateViewModelGraph();
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
        using var graph = CreateViewModelGraph();
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
        using var graph = CreateViewModelGraph();
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
        using var graph = CreateViewModelGraph();

        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(null!, graph.RouteViewModels));
        Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(graph.NavigationService, null!));
    }

    [Fact]
    public async Task NavigationAwareViewModelsAreActivatedAndTheirTasksAreTracked()
    {
        using var graph = CreateViewModelGraph();
        var navigationAwareViewModel = new NavigationAwareTestViewModel();
        var routeViewModels = new Dictionary<AppRoute, ViewModelBase>(graph.RouteViewModels)
        {
            [AppRoute.Dashboard] = navigationAwareViewModel
        };
        var mainWindowViewModel = new MainWindowViewModel(
            graph.NavigationService,
            routeViewModels);

        graph.NavigationService.Navigate(AppRoute.Dashboard);

        var firstActivationTask = mainWindowViewModel.CurrentActivationTask;
        Assert.Equal(1, navigationAwareViewModel.ActivationCount);
        Assert.Same(navigationAwareViewModel.PendingTask, firstActivationTask);
        Assert.False(firstActivationTask.IsCompleted);

        navigationAwareViewModel.CompleteActivation();
        await firstActivationTask;

        graph.NavigationService.Navigate(AppRoute.Goal);
        Assert.Equal(1, navigationAwareViewModel.ActivationCount);
        Assert.Same(Task.CompletedTask, mainWindowViewModel.CurrentActivationTask);

        graph.NavigationService.Navigate(AppRoute.Dashboard);

        var secondActivationTask = mainWindowViewModel.CurrentActivationTask;
        Assert.Equal(2, navigationAwareViewModel.ActivationCount);
        Assert.NotSame(firstActivationTask, secondActivationTask);
        navigationAwareViewModel.CompleteActivation();
        await secondActivationTask;
    }

    [Fact]
    public void AuthenticatedShellPresentationPropertiesFollowTheCurrentRoute()
    {
        using var graph = CreateViewModelGraph();
        var mainWindowViewModel = new MainWindowViewModel(
            graph.NavigationService,
            graph.AuthenticationService,
            graph.RouteViewModels);

        Assert.True(mainWindowViewModel.IsAuthenticationRoute);
        Assert.False(mainWindowViewModel.IsAuthenticatedRoute);

        graph.NavigationService.Navigate(AppRoute.Register);
        Assert.True(mainWindowViewModel.IsAuthenticationRoute);
        Assert.False(mainWindowViewModel.IsAuthenticatedRoute);

        graph.NavigationService.Navigate(AppRoute.Dashboard);
        Assert.False(mainWindowViewModel.IsAuthenticationRoute);
        Assert.True(mainWindowViewModel.IsAuthenticatedRoute);
        Assert.True(mainWindowViewModel.IsDashboardActive);
        Assert.False(mainWindowViewModel.IsGoalActive);
        Assert.False(mainWindowViewModel.IsRecordActivityActive);

        graph.NavigationService.Navigate(AppRoute.Goal);
        Assert.True(mainWindowViewModel.IsGoalActive);
        Assert.False(mainWindowViewModel.IsDashboardActive);

        graph.NavigationService.Navigate(AppRoute.RecordActivity);
        Assert.True(mainWindowViewModel.IsRecordActivityActive);
        Assert.False(mainWindowViewModel.IsGoalActive);
    }

    [Fact]
    public void SidebarCommandsNavigateUsingTheExistingNavigationService()
    {
        using var graph = CreateViewModelGraph();
        var mainWindowViewModel = new MainWindowViewModel(
            graph.NavigationService,
            graph.AuthenticationService,
            graph.RouteViewModels);

        mainWindowViewModel.NavigateDashboardCommand.Execute(null);
        Assert.Equal(AppRoute.Dashboard, graph.NavigationService.CurrentRoute);

        mainWindowViewModel.NavigateGoalCommand.Execute(null);
        Assert.Equal(AppRoute.Goal, graph.NavigationService.CurrentRoute);

        mainWindowViewModel.NavigateRecordActivityCommand.Execute(null);
        Assert.Equal(AppRoute.RecordActivity, graph.NavigationService.CurrentRoute);
    }

    [Fact]
    public async Task ShellLogoutClearsTheSharedSessionAndNavigatesToLogin()
    {
        using var graph = CreateViewModelGraph();
        var registration = await graph.AuthenticationService.RegisterAsync(
            "Task12Shell",
            "FitTask12Abc");
        Assert.True(registration.IsSuccess);

        var login = await graph.AuthenticationService.LoginAsync(
            "Task12Shell",
            "FitTask12Abc",
            DateTimeOffset.UtcNow);
        Assert.True(login.IsSuccess);

        var mainWindowViewModel = new MainWindowViewModel(
            graph.NavigationService,
            graph.AuthenticationService,
            graph.RouteViewModels);
        graph.NavigationService.Navigate(AppRoute.Dashboard);

        Assert.Equal("Task12Shell", mainWindowViewModel.AuthenticatedUsername);

        mainWindowViewModel.ShellLogoutCommand.Execute(null);

        Assert.Null(graph.AuthenticationService.CurrentUser);
        Assert.Equal(AppRoute.Login, graph.NavigationService.CurrentRoute);
        Assert.Equal(string.Empty, mainWindowViewModel.AuthenticatedUsername);
    }

    private static ViewModelGraph CreateViewModelGraph()
    {
        var database = RepositoryTestDatabase.CreateAsync().GetAwaiter().GetResult();
        var authenticationService = new AuthenticationService(database.Users);
        var navigationService = new NavigationService();
        var loginViewModel = new LoginViewModel(
            authenticationService,
            navigationService,
            static () => DateTimeOffset.UtcNow);
        var registerViewModel = new RegisterViewModel(
            authenticationService,
            navigationService);
        var dashboardViewModel = new RouteTestViewModel(
            AppRoute.Dashboard,
            "Dashboard");
        var goalViewModel = new RouteTestViewModel(
            AppRoute.Goal,
            "Set Daily Goal");
        var recordActivityViewModel = new RouteTestViewModel(
            AppRoute.RecordActivity,
            "Record Activity");
        var routeViewModels = new Dictionary<AppRoute, ViewModelBase>
        {
            [AppRoute.Login] = loginViewModel,
            [AppRoute.Register] = registerViewModel,
            [AppRoute.Dashboard] = dashboardViewModel,
            [AppRoute.Goal] = goalViewModel,
            [AppRoute.RecordActivity] = recordActivityViewModel
        };

        return new ViewModelGraph(
            database,
            navigationService,
            authenticationService,
            loginViewModel,
            registerViewModel,
            dashboardViewModel,
            goalViewModel,
            recordActivityViewModel,
            routeViewModels);
    }

    private sealed record ViewModelGraph(
        RepositoryTestDatabase Database,
        NavigationService NavigationService,
        AuthenticationService AuthenticationService,
        LoginViewModel LoginViewModel,
        RegisterViewModel RegisterViewModel,
        RouteTestViewModel DashboardViewModel,
        RouteTestViewModel GoalViewModel,
        RouteTestViewModel RecordActivityViewModel,
        IReadOnlyDictionary<AppRoute, ViewModelBase> RouteViewModels) : IDisposable
    {
        public void Dispose()
        {
            Database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private sealed class NavigationAwareTestViewModel : ViewModelBase, INavigationAware
    {
        private TaskCompletionSource<bool>? _completionSource;

        public int ActivationCount { get; private set; }

        public Task PendingTask => _completionSource?.Task ?? Task.CompletedTask;

        public Task OnNavigatedToAsync()
        {
            ActivationCount++;
            _completionSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            return _completionSource.Task;
        }

        public void CompleteActivation()
        {
            _completionSource!.SetResult(true);
        }
    }
}
