using System;

namespace FitnessApp.Models;

public sealed class FitnessGoal
{
    public FitnessGoal(long userId, double targetCalories, DateTimeOffset updatedAtUtc)
        : this(0, userId, targetCalories, updatedAtUtc)
    {
    }

    internal FitnessGoal(
        long goalId,
        long userId,
        double targetCalories,
        DateTimeOffset updatedAtUtc)
    {
        GoalId = goalId;
        UserId = userId;
        TargetCalories = targetCalories;
        UpdatedAtUtc = updatedAtUtc;
    }

    public long GoalId { get; private set; }

    public long UserId { get; private set; }

    public double TargetCalories { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }
}
