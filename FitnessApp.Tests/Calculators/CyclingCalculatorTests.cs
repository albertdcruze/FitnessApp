using System;
using FitnessApp.Calculators;
using Xunit;

namespace FitnessApp.Tests.Calculators;

public sealed class CyclingCalculatorTests
{
    private readonly CyclingCalculator _calculator = new();

    [Fact]
    public void ReferenceCaseUsesTheApprovedMetAndReferenceWeight()
    {
        var result = _calculator.CalculateCalories(20, 60, 20);

        Assert.Equal(560.00, result);
    }

    [Theory]
    [InlineData(3.0, 4.0)]
    [InlineData(15.9, 4.0)]
    [InlineData(16.0, 6.8)]
    [InlineData(19.1, 6.8)]
    [InlineData(19.2, 8.0)]
    [InlineData(22.3, 8.0)]
    [InlineData(22.4, 10.0)]
    [InlineData(25.5, 10.0)]
    [InlineData(25.6, 12.0)]
    [InlineData(32.1, 12.0)]
    [InlineData(32.2, 16.8)]
    [InlineData(60.0, 16.8)]
    public void SelectsMetUsingInclusiveUpperBounds(double speedKmPerHour, double expectedMet)
    {
        var result = _calculator.CalculateCalories(speedKmPerHour, 60, speedKmPerHour);

        Assert.Equal(expectedMet * 70, result);
    }

    [Fact]
    public void AcceptsCoherentMetricBoundaries()
    {
        Assert.Equal(9.33, _calculator.CalculateCalories(0.1, 2, 3));
        Assert.Equal(5880.00, _calculator.CalculateCalories(300, 300, 60));
        Assert.Equal(4.67, _calculator.CalculateCalories(0.1, 1, 6));
        Assert.Equal(3360.00, _calculator.CalculateCalories(36, 720, 3));
        Assert.Equal(280.00, _calculator.CalculateCalories(3, 60, 3));
        Assert.Equal(1176.00, _calculator.CalculateCalories(60, 60, 60));
    }

    [Theory]
    [InlineData(0.09, 60.0, 3.0)]
    [InlineData(300.1, 300.0, 60.0)]
    [InlineData(20.0, 0.9, 20.0)]
    [InlineData(20.0, 720.1, 20.0)]
    [InlineData(20.0, 60.0, 2.9)]
    [InlineData(20.0, 60.0, 60.1)]
    public void RejectsValuesOutsideMetricRanges(double distanceKm, double durationMinutes, double speedKmPerHour)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateCalories(distanceKm, durationMinutes, speedKmPerHour));
    }

    [Fact]
    public void AcceptsAnExactSpeedAndExactlyTenPercentDifference()
    {
        Assert.Equal(560.00, _calculator.CalculateCalories(20, 60, 20));
        Assert.Equal(560.00, _calculator.CalculateCalories(20, 60, 22));
        Assert.Equal(560.00, _calculator.CalculateCalories(20, 60, 18));
    }

    [Fact]
    public void RejectsSpeedMoreThanTenPercentDifferent()
    {
        Assert.Throws<ArgumentException>(() => _calculator.CalculateCalories(20, 60, 22.01));
    }

    [Theory]
    [InlineData(0.1, 720.0, 3.0)]
    [InlineData(300.0, 1.0, 60.0)]
    public void RejectsUnsupportedCalculatedSpeeds(double distanceKm, double durationMinutes, double speedKmPerHour)
    {
        Assert.Throws<ArgumentException>(
            () => _calculator.CalculateCalories(distanceKm, durationMinutes, speedKmPerHour));
    }
}
