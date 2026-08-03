using System;
using FitnessApp.Common;

namespace FitnessApp.Services;

public sealed class NavigationService
{
    public AppRoute CurrentRoute { get; private set; } = AppRoute.Login;

    public string? CurrentStatusMessage { get; private set; }

    public event EventHandler? NavigationChanged;

    public void Navigate(AppRoute route, string? statusMessage = null)
    {
        if (!Enum.IsDefined(route))
        {
            throw new ArgumentOutOfRangeException(
                nameof(route),
                route,
                "The requested application route is not supported.");
        }

        if (CurrentRoute == route && CurrentStatusMessage == statusMessage)
        {
            return;
        }

        CurrentRoute = route;
        CurrentStatusMessage = statusMessage;
        NavigationChanged?.Invoke(this, EventArgs.Empty);
    }
}
