using System;
using FitTrack.Models;

namespace FitTrack.Calculators;

public sealed class StrengthTrainingCalculator : IActivityCalculator
{
    private const double ReferenceWeightKg = 70.0;

    public ActivityType ActivityType => ActivityType.StrengthTraining;

    /// <summary>
    /// metric1 is duration in minutes, metric2 is total sets, and metric3 is effort level 1, 2, or 3.
    /// </summary>
    public double CalculateCalories(
        double metric1Value,
        double metric2Value,
        double metric3Value)
    {
        ValidateInRange(metric1Value, 1, 180, nameof(metric1Value));
        ValidateWholeNumberInRange(metric2Value, 1, 50, nameof(metric2Value));
        ValidateWholeNumberInRange(metric3Value, 1, 3, nameof(metric3Value));

        var met = SelectMet((int)metric3Value);
        return RoundCalories(met, metric1Value);
    }

    private static double SelectMet(int effortLevel)
    {
        return effortLevel switch
        {
            1 => 3.5,
            2 => 5.0,
            3 => 6.0,
            _ => throw new ArgumentException("The effort level is not supported.")
        };
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
