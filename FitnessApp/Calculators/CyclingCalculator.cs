using System;
using FitnessApp.Models;

namespace FitnessApp.Calculators;

public sealed class CyclingCalculator : IActivityCalculator
{
    private const double ReferenceWeightKg = 70.0;

    public ActivityType ActivityType => ActivityType.Cycling;

    /// <summary>
    /// metric1 is distance in kilometres, metric2 is duration in minutes, and metric3 is entered speed in kilometres per hour.
    /// </summary>
    public double CalculateCalories(
        double metric1Value,
        double metric2Value,
        double metric3Value)
    {
        ValidateInRange(metric1Value, 0.1, 300, nameof(metric1Value));
        ValidateInRange(metric2Value, 1, 720, nameof(metric2Value));
        ValidateInRange(metric3Value, 3, 60, nameof(metric3Value));

        var calculatedSpeedKmPerHour = metric1Value / (metric2Value / 60.0);
        if (calculatedSpeedKmPerHour < 3 || calculatedSpeedKmPerHour > 60)
        {
            throw new ArgumentException(
                "The cycling distance and duration produce an unsupported speed.");
        }

        var allowedSpeedDifference = calculatedSpeedKmPerHour * 0.10;
        var speedDifference = Math.Abs(metric3Value - calculatedSpeedKmPerHour);
        if (speedDifference > allowedSpeedDifference)
        {
            throw new ArgumentException(
                "The entered cycling speed is inconsistent with distance and duration.");
        }

        var met = SelectMet(calculatedSpeedKmPerHour);
        return RoundCalories(met, metric2Value);
    }

    private static double SelectMet(double speedKmPerHour)
    {
        if (speedKmPerHour <= 15.9)
        {
            return 4.0;
        }

        if (speedKmPerHour <= 19.1)
        {
            return 6.8;
        }

        if (speedKmPerHour <= 22.3)
        {
            return 8.0;
        }

        if (speedKmPerHour <= 25.5)
        {
            return 10.0;
        }

        if (speedKmPerHour <= 32.1)
        {
            return 12.0;
        }

        return 16.8;
    }

    private static double RoundCalories(double met, double durationMinutes)
    {
        var calories = met * ReferenceWeightKg * durationMinutes / 60.0;
        return Math.Round(calories, 2, MidpointRounding.AwayFromZero);
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
