using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using FitnessApp.Common;
using FitnessApp.Services;

namespace FitnessApp.ViewModels;

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

    [ObservableProperty]
    private AppRoute _currentRoute;

    [ObservableProperty]
    private ViewModelBase _currentViewModel = null!;

    internal Task CurrentActivationTask { get; private set; } = Task.CompletedTask;

    public MainWindowViewModel(
        NavigationService navigationService,
        IReadOnlyDictionary<AppRoute, ViewModelBase> routeViewModels)
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
