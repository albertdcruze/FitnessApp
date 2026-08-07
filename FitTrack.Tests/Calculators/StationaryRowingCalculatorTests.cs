using System;
using FitTrack.Calculators;
using Xunit;

namespace FitTrack.Tests.Calculators;

public sealed class StationaryRowingCalculatorTests
{
    private readonly StationaryRowingCalculator _calculator = new();

    [Fact]
    public void ReferenceCaseUsesTheApprovedMetAndReferenceWeight()
    {
        var result = _calculator.CalculateCalories(30, 150, 25);

        Assert.Equal(385.00, result);
    }

    [Theory]
    [InlineData(20.0, 5.0)]
    [InlineData(99.0, 5.0)]
    [InlineData(100.0, 7.5)]
    [InlineData(149.0, 7.5)]
    [InlineData(150.0, 11.0)]
    [InlineData(199.0, 11.0)]
    [InlineData(200.0, 14.0)]
    [InlineData(400.0, 14.0)]
    public void SelectsMetUsingInclusiveUpperBounds(double averagePowerWatts, double expectedMet)
    {
        var result = _calculator.CalculateCalories(60, averagePowerWatts, 25);

        Assert.Equal(expectedMet * 70, result);
    }

    [Fact]
    public void AcceptsCoherentMetricBoundaries()
    {
        Assert.Equal(5.83, _calculator.CalculateCalories(1, 20, 10));
        Assert.Equal(2940.00, _calculator.CalculateCalories(180, 400, 50));
    }

    [Theory]
    [InlineData(0.9, 150.0, 25.0)]
    [InlineData(180.1, 150.0, 25.0)]
    [InlineData(30.0, 19.9, 25.0)]
    [InlineData(30.0, 400.1, 25.0)]
    [InlineData(30.0, 150.0, 9.9)]
    [InlineData(30.0, 150.0, 50.1)]
    public void RejectsValuesOutsideMetricRanges(double durationMinutes, double averagePowerWatts, double strokeRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateCalories(durationMinutes, averagePowerWatts, strokeRate));
    }
}
