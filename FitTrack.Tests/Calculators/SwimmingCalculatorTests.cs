using System;
using FitTrack.Calculators;
using Xunit;

namespace FitTrack.Tests.Calculators;

public sealed class SwimmingCalculatorTests
{
    private readonly SwimmingCalculator _calculator = new();

    [Fact]
    public void ReferenceCaseUsesTheApprovedMetAndReferenceWeight()
    {
        var result = _calculator.CalculateCalories(80, 40, 140);

        Assert.Equal(373.33, result);
    }

    [Fact]
    public void UsesTheFixedTwentyFiveMetreLapLength()
    {
        var result = _calculator.CalculateCalories(40, 25, 130);

        Assert.Equal(169.17, result);
    }

    [Theory]
    [InlineData(1, 5.8)]
    [InlineData(41, 5.8)]
    [InlineData(42, 8.0)]
    [InlineData(57, 8.0)]
    [InlineData(58, 10.5)]
    [InlineData(82, 10.5)]
    [InlineData(83, 14.5)]
    [InlineData(100, 14.5)]
    public void SelectsMetUsingInclusiveUpperBounds(double speedMetresPerMinute, double expectedMet)
    {
        var result = _calculator.CalculateCalories(speedMetresPerMinute, 25, 140);

        Assert.Equal(Math.Round(expectedMet * 70 * 25 / 60, 2, MidpointRounding.AwayFromZero), result);
    }

    [Fact]
    public void AcceptsCoherentMetricBoundaries()
    {
        Assert.Equal(169.17, _calculator.CalculateCalories(1, 25, 40));
        Assert.Equal(1691.67, _calculator.CalculateCalories(400, 100, 220));
    }

    [Theory]
    [InlineData(0.0, 40.0, 140.0)]
    [InlineData(400.1, 40.0, 140.0)]
    [InlineData(80.0, 0.9, 140.0)]
    [InlineData(80.0, 300.1, 140.0)]
    [InlineData(80.0, 40.0, 39.9)]
    [InlineData(80.0, 40.0, 220.1)]
    public void RejectsValuesOutsideMetricRanges(double laps, double durationMinutes, double heartRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateCalories(laps, durationMinutes, heartRate));
    }

    [Fact]
    public void RejectsFractionalLaps()
    {
        Assert.Throws<ArgumentException>(() => _calculator.CalculateCalories(20.5, 40, 140));
    }

    [Theory]
    [InlineData(400.0, 1.0)]
    [InlineData(1.0, 300.0)]
    public void RejectsUnsupportedDerivedSpeeds(double laps, double durationMinutes)
    {
        Assert.Throws<ArgumentException>(
            () => _calculator.CalculateCalories(laps, durationMinutes, 140));
    }

    [Fact]
    public void HeartRateIsValidatedButDoesNotChangeTheFormula()
    {
        var lowerResult = _calculator.CalculateCalories(80, 40, 40);
        var upperResult = _calculator.CalculateCalories(80, 40, 220);

        Assert.Equal(lowerResult, upperResult);
    }
}
