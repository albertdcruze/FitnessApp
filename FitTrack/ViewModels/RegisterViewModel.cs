using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitTrack.Common;
using FitTrack.Services;

namespace FitTrack.ViewModels;

public partial class RegisterViewModel : ViewModelBase
{
    private const string RegistrationSuccessMessage =
        "Registration successful. You can now sign in.";

    private const string TechnicalFailureMessage =
        "Unable to create the account right now. Please try again.";

    private const string PasswordMismatchMessage =
        "Passwords do not match.";

    private readonly AuthenticationService _authenticationService;
    private readonly NavigationService _navigationService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private bool _isConfirmPasswordVisible;

    public string PasswordVisibilityActionText =>
        IsPasswordVisible ? "Hide password" : "Show password";

    public string ConfirmPasswordVisibilityActionText =>
        IsConfirmPasswordVisible ? "Hide password" : "Show password";

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
            if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
            {
                ErrorMessage = PasswordMismatchMessage;
                return;
            }

            var result = await _authenticationService
                .RegisterAsync(Username, Password);

            if (result.IsSuccess)
            {
                Password = string.Empty;
                ConfirmPassword = string.Empty;
                IsPasswordVisible = false;
                IsConfirmPasswordVisible = false;
                StatusMessage = RegistrationSuccessMessage;
                _navigationService.Navigate(AppRoute.Login, RegistrationSuccessMessage);
                return;
            }

            ErrorMessage = result.ErrorMessage ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            IsPasswordVisible = false;
            IsConfirmPasswordVisible = false;
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
        IsPasswordVisible = false;
        IsConfirmPasswordVisible = false;
        _navigationService.Navigate(AppRoute.Login);
    }

    partial void OnIsPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(PasswordVisibilityActionText));
    }

    partial void OnIsConfirmPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ConfirmPasswordVisibilityActionText));
    }
}
