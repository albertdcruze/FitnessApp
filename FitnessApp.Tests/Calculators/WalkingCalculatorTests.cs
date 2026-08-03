using System;
using FitnessApp.Calculators;
using Xunit;

namespace FitnessApp.Tests.Calculators;

public sealed class WalkingCalculatorTests
{
    private readonly WalkingCalculator _calculator = new();

    [Fact]
    public void ReferenceCaseUsesTheApprovedMetAndReferenceWeight()
    {
        var result = _calculator.CalculateCalories(5000, 4, 60);

        Assert.Equal(245.00, result);
    }

    [Theory]
    [InlineData(0.5, 2.1)]
    [InlineData(1.8, 2.1)]
    [InlineData(1.81, 2.8)]
    [InlineData(3.1, 2.8)]
    [InlineData(3.11, 3.0)]
    [InlineData(3.9, 3.0)]
    [InlineData(3.91, 3.5)]
    [InlineData(4.7, 3.5)]
    [InlineData(4.71, 3.8)]
    [InlineData(5.5, 3.8)]
    [InlineData(5.51, 4.8)]
    [InlineData(6.3, 4.8)]
    [InlineData(6.31, 5.8)]
    [InlineData(7.1, 5.8)]
    [InlineData(7.11, 6.8)]
    [InlineData(7.9, 6.8)]
    [InlineData(7.91, 8.3)]
    [InlineData(8.9, 8.3)]
    public void SelectsMetUsingInclusiveUpperBounds(double speedKmPerHour, double expectedMet)
    {
        var result = _calculator.CalculateCalories(5000, speedKmPerHour, 60);

        Assert.Equal(expectedMet * 70, result);
    }

    [Fact]
    public void AcceptsCoherentMetricBoundaries()
    {
        Assert.Equal(147.00, _calculator.CalculateCalories(1, 0.5, 60));
        Assert.Equal(6972.00, _calculator.CalculateCalories(100000, 100, 720));
    }

    [Theory]
    [InlineData(0.0, 4.0, 60.0)]
    [InlineData(100000.1, 4.0, 60.0)]
    [InlineData(5000.0, 0.09, 60.0)]
    [InlineData(5000.0, 100.1, 60.0)]
    [InlineData(5000.0, 4.0, 0.9)]
    [InlineData(5000.0, 4.0, 720.1)]
    public void RejectsValuesOutsideMetricRanges(double steps, double distanceKm, double durationMinutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateCalories(steps, distanceKm, durationMinutes));
    }

    [Fact]
    public void RejectsFractionalSteps()
    {
        Assert.Throws<ArgumentException>(() => _calculator.CalculateCalories(5000.5, 4, 60));
    }

    [Theory]
    [InlineData(100.0, 1.0)]
    [InlineData(0.1, 720.0)]
    public void RejectsUnsupportedDerivedSpeeds(double distanceKm, double durationMinutes)
    {
        Assert.Throws<ArgumentException>(
            () => _calculator.CalculateCalories(5000, distanceKm, durationMinutes));
    }
}
