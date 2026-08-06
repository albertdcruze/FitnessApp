namespace FitnessApp.ViewModels;

public sealed record RecentActivityItem(
    string ActivityName,
    double Calories,
    string RecordedAtText);
