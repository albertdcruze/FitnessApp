using System;
using Avalonia.Controls;
using FitTrack.ViewModels;

namespace FitTrack.Views;

public partial class AuthenticatedShellView : UserControl
{
    public AuthenticatedShellView()
    {
        InitializeComponent();
        DataContextChanged += OnShellDataContextChanged;
        SizeChanged += OnShellSizeChanged;
    }

    private void OnShellDataContextChanged(object? sender, EventArgs eventArgs)
    {
        UpdateResponsiveState(Bounds.Width);
    }

    private void OnShellSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
    {
        UpdateResponsiveState(eventArgs.NewSize.Width);
    }

    private void UpdateResponsiveState(double width)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.UpdateShellWidth(width);
        }
    }
}
