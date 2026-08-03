using System;
using FitnessApp.Common;
using FitnessApp.ViewModels;

namespace FitnessApp.Tests.ViewModels;

internal sealed class RouteTestViewModel : ViewModelBase
{
    public RouteTestViewModel(AppRoute route, string title)
    {
        if (route is not AppRoute.Dashboard
            and not AppRoute.Goal
            and not AppRoute.RecordActivity)
        {
            throw new ArgumentOutOfRangeException(nameof(route));
        }

        Route = route;
        Title = title;
    }

    public AppRoute Route { get; }

    public string Title { get; }
}
