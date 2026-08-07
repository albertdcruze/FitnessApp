using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FitTrack.Calculators;
using FitTrack.Models;
using Xunit;

namespace FitTrack.Tests.Calculators;

public sealed class ActivityCalculatorContractTests
{
    public static IEnumerable<object[]> CalculatorCases()
    {
        yield return new object[] { new WalkingCalculator(), ActivityType.Walking, 5000.0, 4.0, 60.0 };
        yield return new object[] { new SwimmingCalculator(), ActivityType.Swimming, 80.0, 40.0, 140.0 };
        yield return new object[] { new RunningCalculator(), ActivityType.Running, 5.0, 30.0, 6.0 };
        yield return new object[] { new CyclingCalculator(), ActivityType.Cycling, 20.0, 60.0, 20.0 };
        yield return new object[] { new StationaryRowingCalculator(), ActivityType.StationaryRowing, 30.0, 150.0, 25.0 };
        yield return new object[] { new StrengthTrainingCalculator(), ActivityType.StrengthTraining, 45.0, 12.0, 2.0 };
    }

    public static IEnumerable<object[]> ActivityTypeCases()
    {
        yield return new object[] { new WalkingCalculator(), ActivityType.Walking };
        yield return new object[] { new SwimmingCalculator(), ActivityType.Swimming };
        yield return new object[] { new RunningCalculator(), ActivityType.Running };
        yield return new object[] { new CyclingCalculator(), ActivityType.Cycling };
        yield return new object[] { new StationaryRowingCalculator(), ActivityType.StationaryRowing };
        yield return new object[] { new StrengthTrainingCalculator(), ActivityType.StrengthTraining };
    }

    public static IEnumerable<object[]> CalculatorInputCases()
    {
        foreach (var calculatorCase in CalculatorCases())
        {
            yield return new object[]
            {
                calculatorCase[0],
                calculatorCase[2],
                calculatorCase[3],
                calculatorCase[4]
            };
        }
    }

    public static IEnumerable<object[]> InvalidMetricCases()
    {
        foreach (var calculatorCase in CalculatorCases())
        {
            var calculator = (IActivityCalculator)calculatorCase[0];
            var metrics = new[]
            {
                (double)calculatorCase[2],
                (double)calculatorCase[3],
                (double)calculatorCase[4]
            };

            foreach (var invalidValue in new[]
            {
                0.0,
                -1.0,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity
            })
            {
                for (var metricIndex = 0; metricIndex < 3; metricIndex++)
                {
                    var invalidMetrics = (double[])metrics.Clone();
                    invalidMetrics[metricIndex] = invalidValue;
                    yield return new object[]
                    {
                        calculator,
                        invalidMetrics[0],
                        invalidMetrics[1],
                        invalidMetrics[2]
                    };
                }
            }
        }
    }

    [Fact]
    public void AllSixCalculatorsHaveUniqueActivityAssociations()
    {
        var calculators = CalculatorCases()
            .Select(calculatorCase => (IActivityCalculator)calculatorCase[0])
            .ToArray();

        Assert.Equal(6, calculators.Length);
        Assert.Equal(6, calculators.Select(calculator => calculator.ActivityType).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(ActivityTypeCases))]
    public void ReportsTheApprovedActivityType(
        IActivityCalculator calculator,
        ActivityType expectedActivityType)
    {
        Assert.Equal(expectedActivityType, calculator.ActivityType);
    }

    [Theory]
    [MemberData(nameof(CalculatorInputCases))]
    public void RepeatedCalculationsReturnTheSameValue(
        IActivityCalculator calculator,
        double metric1Value,
        double metric2Value,
        double metric3Value)
    {
        var firstResult = calculator.CalculateCalories(metric1Value, metric2Value, metric3Value);
        var secondResult = calculator.CalculateCalories(metric1Value, metric2Value, metric3Value);

        Assert.Equal(firstResult, secondResult);
    }

    [Theory]
    [MemberData(nameof(InvalidMetricCases))]
    public void RejectsZeroNegativeNaNAndInfinityForEveryMetric(
        IActivityCalculator calculator,
        double metric1Value,
        double metric2Value,
        double metric3Value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => calculator.CalculateCalories(metric1Value, metric2Value, metric3Value));
    }

    [Fact]
    public void NumericCalculationsDoNotDependOnCurrentCulture()
    {
        var calculator = new WalkingCalculator();
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var enUsResult = calculator.CalculateCalories(5000, 4, 60);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var deDeResult = calculator.CalculateCalories(5000, 4, 60);

            Assert.Equal(enUsResult, deDeResult);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void UsesAwayFromZeroForAFinalCalorieMidpoint()
    {
        var calculator = new StationaryRowingCalculator();

        var result = calculator.CalculateCalories(1.002, 20, 25);

        Assert.Equal(5.85, result);
    }
}
