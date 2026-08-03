using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessApp.Common;
using FitnessApp.Services;

namespace FitnessApp.ViewModels;

public partial class RegisterViewModel : ViewModelBase
{
    private const string RegistrationSuccessMessage =
        "Registration successful. You can now sign in.";

    private const string TechnicalFailureMessage =
        "Unable to create the account right now. Please try again.";

    private readonly AuthenticationService _authenticationService;
    private readonly NavigationService _navigationService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public RegisterViewModel(
        AuthenticationService authenticationService,
        NavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(authenticationService);
        ArgumentNullException.ThrowIfNull(navigationService);

        _authenticationService = authenticationService;
        _navigationService = navigationService;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RegisterAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        try
        {
            var result = await _authenticationService
                .RegisterAsync(Username, Password);

            if (result.IsSuccess)
            {
                Password = string.Empty;
                StatusMessage = RegistrationSuccessMessage;
                _navigationService.Navigate(AppRoute.Login, RegistrationSuccessMessage);
                return;
            }

            ErrorMessage = result.ErrorMessage ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            Password = string.Empty;
            ErrorMessage = TechnicalFailureMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BackToLogin()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        _navigationService.Navigate(AppRoute.Login);
    }
}
