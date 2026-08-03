using System;
using FitnessApp.Models;

namespace FitnessApp.Calculators;

public sealed class StationaryRowingCalculator : IActivityCalculator
{
    private const double ReferenceWeightKg = 70.0;

    public ActivityType ActivityType => ActivityType.StationaryRowing;

    /// <summary>
    /// metric1 is duration in minutes, metric2 is average power in watts, and metric3 is stroke rate in strokes per minute.
    /// </summary>
    public double CalculateCalories(
        double metric1Value,
        double metric2Value,
        double metric3Value)
    {
        ValidateInRange(metric1Value, 1, 180, nameof(metric1Value));
        ValidateInRange(metric2Value, 20, 400, nameof(metric2Value));
        ValidateInRange(metric3Value, 10, 50, nameof(metric3Value));

        var met = SelectMet(metric2Value);
        return RoundCalories(met, metric1Value);
    }

    private static double SelectMet(double averagePowerWatts)
    {
        if (averagePowerWatts <= 99)
        {
            return 5.0;
        }

        if (averagePowerWatts <= 149)
        {
            return 7.5;
        }

        if (averagePowerWatts <= 199)
        {
            return 11.0;
        }

        return 14.0;
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
