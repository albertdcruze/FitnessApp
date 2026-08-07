using System;
using FitTrack.Calculators;
using Xunit;

namespace FitTrack.Tests.Calculators;

public sealed class StrengthTrainingCalculatorTests
{
    private readonly StrengthTrainingCalculator _calculator = new();

    [Fact]
    public void ReferenceCaseUsesTheApprovedMetAndReferenceWeight()
    {
        var result = _calculator.CalculateCalories(45, 12, 2);

        Assert.Equal(262.50, result);
    }

    [Theory]
    [InlineData(1.0, 3.5)]
    [InlineData(2.0, 5.0)]
    [InlineData(3.0, 6.0)]
    public void SelectsMetForEachSupportedEffort(double effortLevel, double expectedMet)
    {
        var result = _calculator.CalculateCalories(60, 12, effortLevel);

        Assert.Equal(expectedMet * 70, result);
    }

    [Fact]
    public void AcceptsCoherentMetricBoundaries()
    {
        Assert.Equal(4.08, _calculator.CalculateCalories(1, 1, 1));
        Assert.Equal(1260.00, _calculator.CalculateCalories(180, 50, 3));
    }

    [Theory]
    [InlineData(0.9, 12.0, 2.0)]
    [InlineData(180.1, 12.0, 2.0)]
    [InlineData(45.0, 0.9, 2.0)]
    [InlineData(45.0, 50.1, 2.0)]
    [InlineData(45.0, 12.0, 0.9)]
    [InlineData(45.0, 12.0, 3.1)]
    public void RejectsValuesOutsideMetricRanges(double durationMinutes, double totalSets, double effortLevel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateCalories(durationMinutes, totalSets, effortLevel));
    }

    [Theory]
    [InlineData(10.5, 2.0)]
    [InlineData(10.0, 2.5)]
    public void RejectsFractionalWholeNumberMetrics(double totalSets, double effortLevel)
    {
        Assert.Throws<ArgumentException>(
            () => _calculator.CalculateCalories(45, totalSets, effortLevel));
    }

    [Fact]
    public void TotalSetsAreValidatedButDoNotChangeTheFormula()
    {
        var fewerSets = _calculator.CalculateCalories(45, 1, 2);
        var moreSets = _calculator.CalculateCalories(45, 50, 2);

        Assert.Equal(fewerSets, moreSets);
    }
}
