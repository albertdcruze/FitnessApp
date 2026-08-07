namespace FitTrack.Models;

public sealed class ActivityMetricDefinition
{
    public ActivityMetricDefinition(
        string label,
        string unit,
        double minimum,
        double maximum,
        bool wholeNumberOnly)
    {
        Label = label;
        Unit = unit;
        Minimum = minimum;
        Maximum = maximum;
        WholeNumberOnly = wholeNumberOnly;
    }

    public string Label { get; }

    public string Unit { get; }

    public double Minimum { get; }

    public double Maximum { get; }

    public bool WholeNumberOnly { get; }
}
