namespace FitTrack.Models;

public sealed class ProgressSummary
{
    public ProgressSummary(
        bool hasGoal,
        double targetCalories,
        double totalCalories,
        double remainingCalories,
        double progressPercentage,
        bool isGoalAchieved,
        string statusMessage)
    {
        HasGoal = hasGoal;
        TargetCalories = targetCalories;
        TotalCalories = totalCalories;
        RemainingCalories = remainingCalories;
        ProgressPercentage = progressPercentage;
        IsGoalAchieved = isGoalAchieved;
        StatusMessage = statusMessage;
    }

    public bool HasGoal { get; }

    public double TargetCalories { get; }

    public double TotalCalories { get; }

    public double RemainingCalories { get; }

    public double ProgressPercentage { get; }

    public bool IsGoalAchieved { get; }

    public string StatusMessage { get; }
}
