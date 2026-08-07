using System;
using FitTrack.Models;

namespace FitTrack.Calculators;

public sealed class WalkingCalculator : IActivityCalculator
{
    private const double ReferenceWeightKg = 70.0;

    public ActivityType ActivityType => ActivityType.Walking;

    /// <summary>
    /// metric1 is steps, metric2 is distance in kilometres, and metric3 is duration in minutes.
    /// </summary>
    public double CalculateCalories(
        double metric1Value,
        double metric2Value,
        double metric3Value)
    {
        ValidateWholeNumberInRange(metric1Value, 1, 100000, nameof(metric1Value));
        ValidateInRange(metric2Value, 0.1, 100, nameof(metric2Value));
        ValidateInRange(metric3Value, 1, 720, nameof(metric3Value));

        var speedKmPerHour = metric2Value / (metric3Value / 60.0);
        if (speedKmPerHour < 0.5 || speedKmPerHour > 8.9)
        {
            throw new ArgumentException(
                "The walking distance and duration produce an unsupported speed.");
        }

        var met = SelectMet(speedKmPerHour);
        return RoundCalories(met, metric3Value);
    }

    private static double SelectMet(double speedKmPerHour)
    {
        if (speedKmPerHour <= 1.8)
        {
            return 2.1;
        }

        if (speedKmPerHour <= 3.1)
        {
            return 2.8;
        }

        if (speedKmPerHour <= 3.9)
        {
            return 3.0;
        }

        if (speedKmPerHour <= 4.7)
        {
            return 3.5;
        }

        if (speedKmPerHour <= 5.5)
        {
            return 3.8;
        }

        if (speedKmPerHour <= 6.3)
        {
            return 4.8;
        }

        if (speedKmPerHour <= 7.1)
        {
            return 5.8;
        }

        if (speedKmPerHour <= 7.9)
        {
            return 6.8;
        }

        return 8.3;
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
