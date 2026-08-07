using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FitTrack.Calculators;
using FitTrack.Common;
using FitTrack.Models;
using FitTrack.Repositories;

namespace FitTrack.Services;

public sealed class ActivityService
{
    private const string UnsupportedActivityMessage =
        "The selected activity is not supported.";

    private const string MissingUserMessage =
        "A registered user is required to record an activity.";

    private const string InvalidMetricMessage =
        "The activity metric values are inconsistent.";

    private const string OutOfRangeMetricMessage =
        "The activity metric values are outside the allowed ranges.";

    private readonly ActivityRepository _activityRepository;
    private readonly IReadOnlyDictionary<ActivityType, IActivityCalculator> _calculators;
    private readonly IReadOnlyList<ActivityDefinition> _activityDefinitions;
    private readonly IReadOnlyDictionary<ActivityType, ActivityDefinition> _definitionsByType;

    public ActivityService(
        ActivityRepository activityRepository,
        IEnumerable<IActivityCalculator> calculators)
    {
        ArgumentNullException.ThrowIfNull(activityRepository);
        ArgumentNullException.ThrowIfNull(calculators);

        _activityRepository = activityRepository;
        _calculators = BuildCalculatorLookup(calculators);
        _activityDefinitions = CreateActivityDefinitions();
        _definitionsByType = _activityDefinitions.ToDictionary(
            definition => definition.ActivityType);
    }

    public IReadOnlyList<ActivityDefinition> GetActivityDefinitions()
    {
        return _activityDefinitions;
    }

    public OperationResult<ActivityDefinition> GetActivityDefinition(ActivityType activityType)
    {
        return _definitionsByType.TryGetValue(activityType, out var definition)
            ? OperationResult<ActivityDefinition>.Success(definition)
            : OperationResult<ActivityDefinition>.Failure(UnsupportedActivityMessage);
    }

    public async Task<OperationResult<ActivityRecord>> RecordActivityAsync(
        User? user,
        ActivityType activityType,
        double metric1Value,
        double metric2Value,
        double metric3Value,
        DateTimeOffset recordedAtUtc)
    {
        if (user is null || user.UserId <= 0)
        {
            return OperationResult<ActivityRecord>.Failure(MissingUserMessage);
        }

        if (!_calculators.TryGetValue(activityType, out var calculator)
            || !_definitionsByType.TryGetValue(activityType, out var definition))
        {
            return OperationResult<ActivityRecord>.Failure(UnsupportedActivityMessage);
        }

        double calories;
        try
        {
            calories = calculator.CalculateCalories(metric1Value, metric2Value, metric3Value);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return OperationResult<ActivityRecord>.Failure(
                CreateOutOfRangeMessage(definition, exception));
        }
        catch (ArgumentException exception)
        {
            return OperationResult<ActivityRecord>.Failure(
                CreateArgumentMessage(definition, exception));
        }

        var normalizedRecordedAtUtc = recordedAtUtc.ToUniversalTime();
        var unsavedRecord = new ActivityRecord(
            user.UserId,
            activityType,
            metric1Value,
            metric2Value,
            metric3Value,
            calories,
            normalizedRecordedAtUtc);

        var activityRecordId = await _activityRepository
            .AddAsync(unsavedRecord)
            .ConfigureAwait(false);

        var storedRecord = new ActivityRecord(
            activityRecordId,
            user.UserId,
            activityType,
            metric1Value,
            metric2Value,
            metric3Value,
            calories,
            normalizedRecordedAtUtc);

        return OperationResult<ActivityRecord>.Success(storedRecord);
    }

    private static IReadOnlyDictionary<ActivityType, IActivityCalculator> BuildCalculatorLookup(
        IEnumerable<IActivityCalculator> calculators)
    {
        var calculatorLookup = new Dictionary<ActivityType, IActivityCalculator>();

        foreach (var calculator in calculators)
        {
            if (calculator is null)
            {
                throw new ArgumentNullException(
                    nameof(calculators),
                    "The calculator collection cannot contain null entries.");
            }

            if (!Enum.IsDefined(calculator.ActivityType))
            {
                throw new InvalidOperationException(
                    $"The calculator activity type '{calculator.ActivityType}' is not supported.");
            }

            if (!calculatorLookup.TryAdd(calculator.ActivityType, calculator))
            {
                throw new InvalidOperationException(
                    $"More than one calculator is registered for {calculator.ActivityType}.");
            }
        }

        foreach (var activityType in Enum.GetValues<ActivityType>())
        {
            if (!calculatorLookup.ContainsKey(activityType))
            {
                throw new InvalidOperationException(
                    $"A calculator is not registered for {activityType}.");
            }
        }

        return new Dictionary<ActivityType, IActivityCalculator>(calculatorLookup);
    }

    private static IReadOnlyList<ActivityDefinition> CreateActivityDefinitions()
    {
        return new List<ActivityDefinition>
        {
            new(
                ActivityType.Walking,
                "Walking",
                new[]
                {
                    new ActivityMetricDefinition("Steps", "steps", 1, 100000, true),
                    new ActivityMetricDefinition("Distance", "km", 0.1, 100, false),
                    new ActivityMetricDefinition("Duration", "minutes", 1, 720, false)
                }),
            new(
                ActivityType.Swimming,
                "Swimming",
                new[]
                {
                    new ActivityMetricDefinition("Laps", "laps", 1, 400, true),
                    new ActivityMetricDefinition("Duration", "minutes", 1, 300, false),
                    new ActivityMetricDefinition("Average heart rate", "bpm", 40, 220, false)
                }),
            new(
                ActivityType.Running,
                "Running",
                new[]
                {
                    new ActivityMetricDefinition("Distance", "km", 0.1, 100, false),
                    new ActivityMetricDefinition("Duration", "minutes", 1, 720, false),
                    new ActivityMetricDefinition("Average pace", "min/km", 3, 15, false)
                }),
            new(
                ActivityType.Cycling,
                "Cycling",
                new[]
                {
                    new ActivityMetricDefinition("Distance", "km", 0.1, 300, false),
                    new ActivityMetricDefinition("Duration", "minutes", 1, 720, false),
                    new ActivityMetricDefinition("Average speed", "km/h", 3, 60, false)
                }),
            new(
                ActivityType.StationaryRowing,
                "Stationary Rowing",
                new[]
                {
                    new ActivityMetricDefinition("Duration", "minutes", 1, 180, false),
                    new ActivityMetricDefinition("Average power", "watts", 20, 400, false),
                    new ActivityMetricDefinition("Stroke rate", "strokes/min", 10, 50, false)
                }),
            new(
                ActivityType.StrengthTraining,
                "Strength Training",
                new[]
                {
                    new ActivityMetricDefinition("Duration", "minutes", 1, 180, false),
                    new ActivityMetricDefinition("Total sets", "sets", 1, 50, true),
                    new ActivityMetricDefinition("Effort level", "level", 1, 3, true)
                })
        }.AsReadOnly();
    }

    private static string CreateOutOfRangeMessage(
        ActivityDefinition definition,
        ArgumentOutOfRangeException exception)
    {
        var metricLabel = GetMetricLabel(definition, exception.ParamName);
        return metricLabel is null
            ? OutOfRangeMetricMessage
            : $"{metricLabel} is outside the allowed range.";
    }

    private static string CreateArgumentMessage(
        ActivityDefinition definition,
        ArgumentException exception)
    {
        var metricLabel = GetMetricLabel(definition, exception.ParamName);
        return metricLabel is null
            ? InvalidMetricMessage
            : $"{metricLabel} is invalid.";
    }

    private static string? GetMetricLabel(
        ActivityDefinition definition,
        string? parameterName)
    {
        return parameterName switch
        {
            "metric1Value" => definition.Metrics[0].Label,
            "metric2Value" => definition.Metrics[1].Label,
            "metric3Value" => definition.Metrics[2].Label,
            _ => null
        };
    }
}
