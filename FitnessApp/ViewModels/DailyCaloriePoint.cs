using System;

namespace FitnessApp.ViewModels;

public sealed record DailyCaloriePoint(
    DateOnly Date,
    string DayLabel,
    double TotalCalories,
    double RelativeBarHeight,
    bool IsToday,
    string ToolTipText);
