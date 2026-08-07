using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitTrack.Common;
using FitTrack.Services;

namespace FitTrack.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly AppRoute[] RequiredRoutes =
    [
        AppRoute.Login,
        AppRoute.Register,
        AppRoute.Dashboard,
        AppRoute.Goal,
        AppRoute.RecordActivity
    ];

    private readonly NavigationService _navigationService;
    private readonly IReadOnlyDictionary<AppRoute, ViewModelBase> _routeViewModels;
    private readonly AuthenticationService? _authenticationService;
    private bool _wideSidebarExpanded = true;

    [ObservableProperty]
    private AppRoute _currentRoute;

    [ObservableProperty]
    private ViewModelBase _currentViewModel = null!;

    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    [ObservableProperty]
    private bool _isCompactShell;

    internal Task CurrentActivationTask { get; private set; } = Task.CompletedTask;

    public bool IsAuthenticationRoute => CurrentRoute is AppRoute.Login or AppRoute.Register;

    public bool IsAuthenticatedRoute => CurrentRoute is
        AppRoute.Dashboard or AppRoute.Goal or AppRoute.RecordActivity;

    public bool IsDashboardActive => CurrentRoute == AppRoute.Dashboard;

    public bool IsGoalActive => CurrentRoute == AppRoute.Goal;

    public bool IsRecordActivityActive => CurrentRoute == AppRoute.RecordActivity;

    public string AuthenticatedUsername => _authenticationService?.CurrentUser?.Username ?? string.Empty;

    public string CurrentRouteName => CurrentRoute.ToString();

    public string CurrentPageTitle => CurrentRoute switch
    {
        AppRoute.Dashboard => "Dashboard",
        AppRoute.Goal => "Set Goal",
        AppRoute.RecordActivity => "Record Activity",
        _ => string.Empty
    };

    public MainWindowViewModel(
        NavigationService navigationService,
        IReadOnlyDictionary<AppRoute, ViewModelBase> routeViewModels)
        : this(navigationService, routeViewModels, null)
    {
    }

    public MainWindowViewModel(
        NavigationService navigationService,
        AuthenticationService authenticationService,
        IReadOnlyDictionary<AppRoute, ViewModelBase> routeViewModels)
        : this(navigationService, routeViewModels, authenticationService)
    {
        ArgumentNullException.ThrowIfNull(authenticationService);
    }

    private MainWindowViewModel(
        NavigationService navigationService,
        IReadOnlyDictionary<AppRoute, ViewModelBase> routeViewModels,
        AuthenticationService? authenticationService)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(routeViewModels);

        foreach (var route in RequiredRoutes)
        {
            if (!routeViewModels.TryGetValue(route, out var viewModel)
                || viewModel is null)
            {
                throw new ArgumentException(
                    $"A ViewModel mapping is required for the {route} route.",
                    nameof(routeViewModels));
            }
        }

        _navigationService = navigationService;
        _routeViewModels = routeViewModels;
        _authenticationService = authenticationService;
        _currentRoute = _navigationService.CurrentRoute;
        _currentViewModel = GetViewModel(_currentRoute);
        ActivateCurrentViewModel();

        _navigationService.NavigationChanged += OnNavigationChanged;
    }

    private void OnNavigationChanged(object? sender, EventArgs eventArgs)
    {
        CurrentRoute = _navigationService.CurrentRoute;
        CurrentViewModel = GetViewModel(CurrentRoute);
        ActivateCurrentViewModel();
    }

    [RelayCommand]
    private void NavigateDashboard()
    {
        _navigationService.Navigate(AppRoute.Dashboard);
    }

    [RelayCommand]
    private void NavigateGoal()
    {
        _navigationService.Navigate(AppRoute.Goal);
    }

    [RelayCommand]
    private void NavigateRecordActivity()
    {
        _navigationService.Navigate(AppRoute.RecordActivity);
    }

    [RelayCommand]
    private void ShellLogout()
    {
        _authenticationService?.Logout();
        _navigationService.Navigate(AppRoute.Login);
    }

    [RelayCommand(CanExecute = nameof(CanToggleSidebar))]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
        _wideSidebarExpanded = IsSidebarExpanded;
    }

    private bool CanToggleSidebar()
    {
        return !IsCompactShell;
    }

    public void UpdateShellWidth(double width)
    {
        var compactShell = width < 768;
        if (IsCompactShell == compactShell)
        {
            return;
        }

        IsCompactShell = compactShell;
        IsSidebarExpanded = compactShell ? false : _wideSidebarExpanded;
        ToggleSidebarCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentRouteChanged(AppRoute value)
    {
        OnPropertyChanged(nameof(IsAuthenticationRoute));
        OnPropertyChanged(nameof(IsAuthenticatedRoute));
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsGoalActive));
        OnPropertyChanged(nameof(IsRecordActivityActive));
        OnPropertyChanged(nameof(AuthenticatedUsername));
        OnPropertyChanged(nameof(CurrentRouteName));
        OnPropertyChanged(nameof(CurrentPageTitle));
    }

    private void ActivateCurrentViewModel()
    {
        CurrentActivationTask = CurrentViewModel is INavigationAware navigationAware
            ? navigationAware.OnNavigatedToAsync()
            : Task.CompletedTask;
    }

    private ViewModelBase GetViewModel(AppRoute route)
    {
        if (!_routeViewModels.TryGetValue(route, out var viewModel)
            || viewModel is null)
        {
            throw new InvalidOperationException(
                $"No ViewModel has been configured for the {route} route.");
        }

        return viewModel;
    }
}
