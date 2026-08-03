using System;
using System.Threading.Tasks;
using FitnessApp.Common;
using FitnessApp.Models;
using FitnessApp.Repositories;

namespace FitnessApp.Services;

public sealed class GoalService
{
    private const string InvalidGoalMessage =
        "Goal must be between 1 and 5,000 calories.";

    private const string MissingUserMessage =
        "A registered user is required to manage a goal.";

    private readonly GoalRepository _goalRepository;

    public GoalService(GoalRepository goalRepository)
    {
        ArgumentNullException.ThrowIfNull(goalRepository);
        _goalRepository = goalRepository;
    }

    public OperationResult<int> ValidateGoal(int targetCalories)
    {
        return targetCalories is < 1 or > 5000
            ? OperationResult<int>.Failure(InvalidGoalMessage)
            : OperationResult<int>.Success(targetCalories);
    }

    public async Task<OperationResult<FitnessGoal?>> GetGoalAsync(User? user)
    {
        if (user is null || user.UserId <= 0)
        {
            return OperationResult<FitnessGoal?>.Failure(MissingUserMessage);
        }

        var goal = await _goalRepository
            .GetByUserIdAsync(user.UserId)
            .ConfigureAwait(false);
        return OperationResult<FitnessGoal?>.Success(goal);
    }

    public async Task<OperationResult<FitnessGoal>> SaveGoalAsync(
        User? user,
        int targetCalories,
        DateTimeOffset updatedAtUtc)
    {
        if (user is null || user.UserId <= 0)
        {
            return OperationResult<FitnessGoal>.Failure(MissingUserMessage);
        }

        var validationResult = ValidateGoal(targetCalories);
        if (!validationResult.IsSuccess)
        {
            return OperationResult<FitnessGoal>.Failure(validationResult.ErrorMessage!);
        }

        var goal = new FitnessGoal(
            user.UserId,
            targetCalories,
            updatedAtUtc.ToUniversalTime());
        var storedGoal = await _goalRepository
            .SaveAsync(goal)
            .ConfigureAwait(false);

        return OperationResult<FitnessGoal>.Success(storedGoal);
    }
}
