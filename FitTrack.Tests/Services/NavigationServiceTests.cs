using System;
using FitTrack.Common;
using FitTrack.Services;
using Xunit;

namespace FitTrack.Tests.Services;

public sealed class NavigationServiceTests
{
    [Fact]
    public void Constructor_StartsOnLoginWithoutAStatusMessage()
    {
        var service = new NavigationService();

        Assert.Equal(AppRoute.Login, service.CurrentRoute);
        Assert.Null(service.CurrentStatusMessage);
    }

    [Theory]
    [InlineData(AppRoute.Register)]
    [InlineData(AppRoute.Login)]
    [InlineData(AppRoute.Dashboard)]
    [InlineData(AppRoute.Goal)]
    [InlineData(AppRoute.RecordActivity)]
    public void Navigate_SupportsEveryApprovedRoute(AppRoute route)
    {
        var service = new NavigationService();

        service.Navigate(route);

        Assert.Equal(route, service.CurrentRoute);
        Assert.Null(service.CurrentStatusMessage);
    }

    [Fact]
    public void Navigate_RaisesAnEventForAValidStateChange()
    {
        var service = new NavigationService();
        var eventCount = 0;
        service.NavigationChanged += (_, _) => eventCount++;

        service.Navigate(AppRoute.Register);

        Assert.Equal(1, eventCount);
        Assert.Equal(AppRoute.Register, service.CurrentRoute);
    }

    [Fact]
    public void Navigate_DoesNotRaiseAnEventForAnIdenticalState()
    {
        var service = new NavigationService();
        var eventCount = 0;
        service.NavigationChanged += (_, _) => eventCount++;

        service.Navigate(AppRoute.Login);

        Assert.Equal(0, eventCount);
        Assert.Equal(AppRoute.Login, service.CurrentRoute);
        Assert.Null(service.CurrentStatusMessage);
    }

    [Fact]
    public void Navigate_RaisesAnEventWhenOnlyTheStatusMessageChanges()
    {
        var service = new NavigationService();
        var eventCount = 0;
        service.NavigationChanged += (_, _) => eventCount++;

        service.Navigate(AppRoute.Login, "Registration successful.");

        Assert.Equal(1, eventCount);
        Assert.Equal(AppRoute.Login, service.CurrentRoute);
        Assert.Equal("Registration successful.", service.CurrentStatusMessage);
    }

    [Fact]
    public void Navigate_WithoutAStatusMessageClearsAnEarlierNavigationMessage()
    {
        var service = new NavigationService();
        service.Navigate(AppRoute.Login, "Registration successful.");

        service.Navigate(AppRoute.Register);

        Assert.Equal(AppRoute.Register, service.CurrentRoute);
        Assert.Null(service.CurrentStatusMessage);
    }

    [Fact]
    public void Navigate_RejectsUnsupportedRoutesWithoutChangingState()
    {
        var service = new NavigationService();
        var eventCount = 0;
        service.NavigationChanged += (_, _) => eventCount++;
        service.Navigate(AppRoute.Register, "Existing message.");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.Navigate((AppRoute)999));

        Assert.Equal("route", exception.ParamName);
        Assert.Equal(AppRoute.Register, service.CurrentRoute);
        Assert.Equal("Existing message.", service.CurrentStatusMessage);
        Assert.Equal(1, eventCount);
    }
}
