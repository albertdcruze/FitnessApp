using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitnessApp.Common;
using FitnessApp.Models;
using FitnessApp.Repositories;

namespace FitnessApp.Services;

public sealed class ProgressService
{
    private const string MissingUserMessage =
        "A registered user is required to view progress.";

    private const string MissingTimeZoneMessage =
        "A valid time zone is required to calculate daily progress.";

    private const string InvalidDayMessage =
        "The selected local date cannot be converted for this time zone.";

    private const string NoGoalMessage =
        "No daily calorie goal has been set.";

    private const string GoalAchievedMessage =
        "Goal achieved.";

    private const string GoalNotAchievedMessage =
        "Goal not achieved yet.";

    private readonly GoalRepository _goalRepository;
    private readonly ActivityRepository _activityRepository;

    public ProgressService(
        GoalRepository goalRepository,
        ActivityRepository activityRepository)
    {
        ArgumentNullException.ThrowIfNull(goalRepository);
        ArgumentNullException.ThrowIfNull(activityRepository);

        _goalRepository = goalRepository;
        _activityRepository = activityRepository;
    }

    public async Task<OperationResult<ProgressSummary>> GetTodayProgressAsync(
        User? user,
        DateOnly localDate,
        TimeZoneInfo? timeZone)
    {
        if (user is null || user.UserId <= 0)
        {
            return OperationResult<ProgressSummary>.Failure(MissingUserMessage);
        }

        if (timeZone is null)
        {
            return OperationResult<ProgressSummary>.Failure(MissingTimeZoneMessage);
        }

        var dayRangeResult = GetUtcDayRange(localDate, timeZone);
        if (!dayRangeResult.IsSuccess)
        {
            return OperationResult<ProgressSummary>.Failure(dayRangeResult.ErrorMessage!);
        }

        var dayRange = dayRangeResult.Value;
        var goal = await _goalRepository
            .GetByUserIdAsync(user.UserId)
            .ConfigureAwait(false);
        var totalCalories = await _activityRepository
            .GetCaloriesTotalAsync(
                user.UserId,
                dayRange.StartUtc,
                dayRange.EndUtc)
            .ConfigureAwait(false);

        var summary = goal is null
            ? new ProgressSummary(
                false,
                0,
                totalCalories,
                0,
                0,
                false,
                NoGoalMessage)
            : CreateGoalSummary(goal, totalCalories);

        return OperationResult<ProgressSummary>.Success(summary);
    }

    public Task<IReadOnlyList<ActivityRecord>> GetActivitiesAsync(
        long userId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        return _activityRepository.GetForUserInRangeAsync(userId, startUtc, endUtc);
    }

    public Task<IReadOnlyList<ActivityRecord>> GetRecentActivitiesAsync(
        long userId,
        int limit)
    {
        return _activityRepository.GetMostRecentAsync(userId, limit);
    }

    private static OperationResult<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)> GetUtcDayRange(
        DateOnly localDate,
        TimeZoneInfo timeZone)
    {
        DateTime localStart;
        DateTime localEnd;
        try
        {
            localStart = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            localEnd = localStart.AddDays(1);
        }
        catch (ArgumentOutOfRangeException)
        {
            return OperationResult<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)>.Failure(
                InvalidDayMessage);
        }

        if (timeZone.IsInvalidTime(localStart) || timeZone.IsInvalidTime(localEnd))
        {
            return OperationResult<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)>.Failure(
                InvalidDayMessage);
        }

        try
        {
            var startUtc = new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone));
            var endUtc = new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone));

            return OperationResult<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)>.Success(
                (startUtc, endUtc));
        }
        catch (ArgumentException)
        {
            return OperationResult<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)>.Failure(
                InvalidDayMessage);
        }
    }

    private static ProgressSummary CreateGoalSummary(
        FitnessGoal goal,
        double totalCalories)
    {
        var remainingCalories = Math.Max(goal.TargetCalories - totalCalories, 0);
        var progressPercentage = totalCalories / goal.TargetCalories * 100;
        var isGoalAchieved = totalCalories >= goal.TargetCalories;

        return new ProgressSummary(
            true,
            goal.TargetCalories,
            totalCalories,
            remainingCalories,
            progressPercentage,
            isGoalAchieved,
            isGoalAchieved ? GoalAchievedMessage : GoalNotAchievedMessage);
    }
}
