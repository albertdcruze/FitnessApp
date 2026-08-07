using System;
using FitTrack.Models;

namespace FitTrack.Calculators;

public sealed class RunningCalculator : IActivityCalculator
{
    private const double ReferenceWeightKg = 70.0;

    public ActivityType ActivityType => ActivityType.Running;

    /// <summary>
    /// metric1 is distance in kilometres, metric2 is duration in minutes, and metric3 is entered pace in minutes per kilometre.
    /// </summary>
    public double CalculateCalories(
        double metric1Value,
        double metric2Value,
        double metric3Value)
    {
        ValidateInRange(metric1Value, 0.1, 100, nameof(metric1Value));
        ValidateInRange(metric2Value, 1, 720, nameof(metric2Value));
        ValidateInRange(metric3Value, 3, 15, nameof(metric3Value));

        var calculatedPaceMinutesPerKilometre = metric2Value / metric1Value;
        var calculatedSpeedKmPerHour = 60.0 / calculatedPaceMinutesPerKilometre;
        if (calculatedSpeedKmPerHour < 4.2 || calculatedSpeedKmPerHour > 20)
        {
            throw new ArgumentException(
                "The running distance and duration produce an unsupported speed.");
        }

        var allowedPaceDifference = calculatedPaceMinutesPerKilometre * 0.10;
        var paceDifference = Math.Abs(metric3Value - calculatedPaceMinutesPerKilometre);
        if (paceDifference > allowedPaceDifference)
        {
            throw new ArgumentException(
                "The entered running pace is inconsistent with distance and duration.");
        }

        var met = SelectMet(calculatedSpeedKmPerHour);
        return RoundCalories(met, metric2Value);
    }

    private static double SelectMet(double speedKmPerHour)
    {
        if (speedKmPerHour <= 6.0)
        {
            return 3.3;
        }

        if (speedKmPerHour <= 6.8)
        {
            return 6.5;
        }

        if (speedKmPerHour <= 7.7)
        {
            return 7.8;
        }

        if (speedKmPerHour <= 8.4)
        {
            return 8.5;
        }

        if (speedKmPerHour <= 9.3)
        {
            return 9.0;
        }

        if (speedKmPerHour <= 10.1)
        {
            return 9.3;
        }

        if (speedKmPerHour <= 11.0)
        {
            return 10.5;
        }

        if (speedKmPerHour <= 11.7)
        {
            return 11.0;
        }

        if (speedKmPerHour <= 12.5)
        {
            return 11.8;
        }

        if (speedKmPerHour <= 13.4)
        {
            return 12.0;
        }

        if (speedKmPerHour <= 14.1)
        {
            return 12.5;
        }

        if (speedKmPerHour <= 14.8)
        {
            return 13.0;
        }

        if (speedKmPerHour <= 16.1)
        {
            return 14.8;
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
