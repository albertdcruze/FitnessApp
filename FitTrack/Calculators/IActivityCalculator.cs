// completed
using FitTrack.Models;

namespace FitTrack.Calculators;

public interface IActivityCalculator
{
    ActivityType ActivityType { get; }

    /// <summary>
    /// Calculates calories using the activity-specific metric order for the three values.
    /// </summary>
    double CalculateCalories(
        double metric1Value,
        double metric2Value,
        double metric3Value);
}
