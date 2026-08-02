using System;

namespace FitnessApp.Models;

public sealed class FitnessGoal
{
    public FitnessGoal(long userId, double targetCalories, DateTimeOffset updatedAtUtc)
    {
        UserId = userId;
        TargetCalories = targetCalories;
        UpdatedAtUtc = updatedAtUtc;
    }

    public long GoalId { get; private set; }

    public long UserId { get; private set; }

    public double TargetCalories { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }
}
