using System;
using System.Collections.Generic;
using System.Linq;

namespace FitTrack.Models;

public sealed class ActivityDefinition
{
    public ActivityDefinition(
        ActivityType activityType,
        string displayName,
        IEnumerable<ActivityMetricDefinition> metrics)
    {
        var metricList = metrics?.ToList()
            ?? throw new ArgumentNullException(nameof(metrics));

        if (metricList.Count != 3)
        {
            throw new ArgumentException("An activity definition must contain exactly three metrics.", nameof(metrics));
        }

        ActivityType = activityType;
        DisplayName = displayName;
        Metrics = metricList.AsReadOnly();
    }

    public ActivityType ActivityType { get; }

    public string DisplayName { get; }

    public IReadOnlyList<ActivityMetricDefinition> Metrics { get; }

    public string IconGlyph => ActivityType switch
    {
        ActivityType.Walking => "\uE21E",
        ActivityType.Swimming => "\uE283",
        ActivityType.Running => "\uE3BD",
        ActivityType.Cycling => "\uE1D2",
        ActivityType.StationaryRowing => "\uE640",
        ActivityType.StrengthTraining => "\uE3A5",
        _ => "\uE038"
    };
}
