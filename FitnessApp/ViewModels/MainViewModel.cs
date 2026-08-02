using CommunityToolkit.Mvvm.ComponentModel;

namespace FitnessApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Greeting { get; set; } = "Welcome to FitnessApp!";
}