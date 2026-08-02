using System;
using System.Collections.Generic;
using System.Linq;

namespace FitnessApp.Models;

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
}
