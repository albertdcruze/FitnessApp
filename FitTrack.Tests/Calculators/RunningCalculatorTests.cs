using System;
using FitTrack.Calculators;
using Xunit;

namespace FitTrack.Tests.Calculators;

public sealed class RunningCalculatorTests
{
    private readonly RunningCalculator _calculator = new();

    [Fact]
    public void ReferenceCaseUsesTheApprovedMetAndReferenceWeight()
    {
        var result = _calculator.CalculateCalories(5, 30, 6);

        Assert.Equal(325.50, result);
    }

    [Theory]
    [InlineData(6.0, 3.3)]
    [InlineData(6.01, 6.5)]
    [InlineData(6.8, 6.5)]
    [InlineData(6.81, 7.8)]
    [InlineData(10.1, 9.3)]
    [InlineData(10.11, 10.5)]
    [InlineData(16.1, 14.8)]
    [InlineData(16.11, 16.8)]
    [InlineData(20.0, 16.8)]
    public void SelectsMetUsingInclusiveUpperBounds(double speedKmPerHour, double expectedMet)
    {
        var distanceKm = speedKmPerHour;
        var result = _calculator.CalculateCalories(
            distanceKm,
            60,
            60 / speedKmPerHour);

        Assert.Equal(expectedMet * 70, result);
    }

    [Fact]
    public void AcceptsCoherentMetricBoundaries()
    {
        Assert.Equal(5.50, _calculator.CalculateCalories(0.1, 60.0 / 4.2 * 0.1, 60.0 / 4.2));
        Assert.Equal(5880.00, _calculator.CalculateCalories(100, 300, 3));
        Assert.Equal(3.85, _calculator.CalculateCalories(0.1, 1, 10));
        Assert.Equal(2772.00, _calculator.CalculateCalories(50.4, 720, 60.0 / 4.2));
        Assert.Equal(294.00, _calculator.CalculateCalories(5, 15, 3));
        Assert.Equal(231.00, _calculator.CalculateCalories(4.2, 60, 15));
    }

    [Theory]
    [InlineData(0.09, 30.0, 6.0)]
    [InlineData(100.1, 300.0, 3.0)]
    [InlineData(5.0, 0.9, 6.0)]
    [InlineData(5.0, 720.1, 6.0)]
    [InlineData(5.0, 30.0, 2.9)]
    [InlineData(5.0, 30.0, 15.1)]
    public void RejectsValuesOutsideMetricRanges(double distanceKm, double durationMinutes, double pace)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateCalories(distanceKm, durationMinutes, pace));
    }

    [Fact]
    public void AcceptsAnExactPaceAndExactlyTenPercentDifference()
    {
        Assert.Equal(325.50, _calculator.CalculateCalories(5, 30, 6));
        Assert.Equal(325.50, _calculator.CalculateCalories(5, 30, 6.6));
        Assert.Equal(325.50, _calculator.CalculateCalories(5, 30, 5.4));
    }

    [Fact]
    public void RejectsPaceMoreThanTenPercentDifferent()
    {
        Assert.Throws<ArgumentException>(() => _calculator.CalculateCalories(5, 30, 6.61));
    }

    [Theory]
    [InlineData(0.1, 720.0, 15.0)]
    [InlineData(100.0, 1.0, 3.0)]
    public void RejectsUnsupportedCalculatedSpeeds(double distanceKm, double durationMinutes, double pace)
    {
        Assert.Throws<ArgumentException>(
            () => _calculator.CalculateCalories(distanceKm, durationMinutes, pace));
    }
}
