using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FitnessApp.Models;

namespace FitnessApp.Converters;

public sealed class ActivityImageConverter : IValueConverter
{
    private static readonly Lazy<IReadOnlyDictionary<ActivityType, IImage>> Images =
        new(CreateImages);

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is ActivityType activityType
            && Images.Value.TryGetValue(activityType, out var image)
                ? image
                : AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return AvaloniaProperty.UnsetValue;
    }

    private static IReadOnlyDictionary<ActivityType, IImage> CreateImages()
    {
        return new Dictionary<ActivityType, IImage>
        {
            [ActivityType.Walking] = Load("walking.png"),
            [ActivityType.Swimming] = Load("swimming.png"),
            [ActivityType.Running] = Load("running.png"),
            [ActivityType.Cycling] = Load("cycling.png"),
            [ActivityType.StationaryRowing] = Load("stationary-rowing.png"),
            [ActivityType.StrengthTraining] = Load("strength-training.png")
        };
    }

    private static Bitmap Load(string fileName)
    {
        var uri = new Uri(
            $"avares://FitnessApp/Assets/Images/Activities/{fileName}");
        using var stream = AssetLoader.Open(uri);
        return new Bitmap(stream);
    }
}
