using System;

namespace FitTrack.Models;

public sealed class ActivityRecord
{
    public ActivityRecord(
        long userId,
        ActivityType activityType,
        double metric1Value,
        double metric2Value,
        double metric3Value,
        double caloriesBurned,
        DateTimeOffset recordedAtUtc)
        : this(
            0,
            userId,
            activityType,
            metric1Value,
            metric2Value,
            metric3Value,
            caloriesBurned,
            recordedAtUtc)
    {
    }

    internal ActivityRecord(
        long activityRecordId,
        long userId,
        ActivityType activityType,
        double metric1Value,
        double metric2Value,
        double metric3Value,
        double caloriesBurned,
        DateTimeOffset recordedAtUtc)
    {
        ActivityRecordId = activityRecordId;
        UserId = userId;
        ActivityType = activityType;
        Metric1Value = metric1Value;
        Metric2Value = metric2Value;
        Metric3Value = metric3Value;
        CaloriesBurned = caloriesBurned;
        RecordedAtUtc = recordedAtUtc;
    }

    public long ActivityRecordId { get; private set; }

    public long UserId { get; private set; }

    public ActivityType ActivityType { get; private set; }

    public double Metric1Value { get; private set; }

    public double Metric2Value { get; private set; }

    public double Metric3Value { get; private set; }

    public double CaloriesBurned { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }
}
