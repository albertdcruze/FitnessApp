using System;
using CommunityToolkit.Mvvm.Input;
using FitnessApp.Common;
using FitnessApp.Services;

namespace FitnessApp.ViewModels;

public partial class AuthenticatedRoutePlaceholderViewModel : ViewModelBase
{
    private readonly AuthenticationService _authenticationService;
    private readonly NavigationService _navigationService;

    public AuthenticatedRoutePlaceholderViewModel(
        AppRoute route,
        string title,
        AuthenticationService authenticationService,
        NavigationService navigationService)
    {
        if (route is not AppRoute.Dashboard
            and not AppRoute.Goal
            and not AppRoute.RecordActivity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(route),
                route,
                "Only authenticated application routes can use a placeholder ViewModel.");
        }

        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(authenticationService);
        ArgumentNullException.ThrowIfNull(navigationService);

        Route = route;
        Title = title;
        _authenticationService = authenticationService;
        _navigationService = navigationService;
    }

    public AppRoute Route { get; }

    public string Title { get; }

    [RelayCommand]
    private void Logout()
    {
        _authenticationService.Logout();
        _navigationService.Navigate(AppRoute.Login);
    }
}
