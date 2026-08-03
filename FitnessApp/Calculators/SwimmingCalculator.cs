using System;
using FitnessApp.Models;

namespace FitnessApp.Calculators;

public sealed class SwimmingCalculator : IActivityCalculator
{
    private const double ReferenceWeightKg = 70.0;
    private const double LapLengthMetres = 25.0;

    public ActivityType ActivityType => ActivityType.Swimming;

    /// <summary>
    /// metric1 is laps, metric2 is duration in minutes, and metric3 is average heart rate in bpm.
    /// </summary>
    public double CalculateCalories(
        double metric1Value,
        double metric2Value,
        double metric3Value)
    {
        ValidateWholeNumberInRange(metric1Value, 1, 400, nameof(metric1Value));
        ValidateInRange(metric2Value, 1, 300, nameof(metric2Value));
        ValidateInRange(metric3Value, 40, 220, nameof(metric3Value));

        var distanceMetres = metric1Value * LapLengthMetres;
        var speedMetresPerMinute = distanceMetres / metric2Value;
        if (speedMetresPerMinute < 1 || speedMetresPerMinute > 100)
        {
            throw new ArgumentException(
                "The swimming laps and duration produce an unsupported speed.");
        }

        var met = SelectMet(speedMetresPerMinute);
        return RoundCalories(met, metric2Value);
    }

    private static double SelectMet(double speedMetresPerMinute)
    {
        if (speedMetresPerMinute <= 41)
        {
            return 5.8;
        }

        if (speedMetresPerMinute <= 57)
        {
            return 8.0;
        }

        if (speedMetresPerMinute <= 82)
        {
            return 10.5;
        }

        return 14.5;
    }

    private static double RoundCalories(double met, double durationMinutes)
    {
        var calories = met * ReferenceWeightKg * durationMinutes / 60.0;
        return Math.Round(calories, 2, MidpointRounding.AwayFromZero);
    }

    private static void ValidateWholeNumberInRange(
        double value,
        double minimum,
        double maximum,
        string parameterName)
    {
        ValidateInRange(value, minimum, maximum, parameterName);
        if (value != Math.Truncate(value))
        {
            throw new ArgumentException(
                "The metric must be a whole number.",
                parameterName);
        }
    }

    private static void ValidateInRange(
        double value,
        double minimum,
        double maximum,
        string parameterName)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The metric is outside its approved range.");
        }
    }
}
