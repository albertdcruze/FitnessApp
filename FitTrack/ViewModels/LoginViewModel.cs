using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitTrack.Common;
using FitTrack.Services;

namespace FitTrack.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private const string TechnicalFailureMessage =
        "Unable to sign in right now. Please try again.";

    private readonly AuthenticationService _authenticationService;
    private readonly NavigationService _navigationService;
    private readonly Func<DateTimeOffset> _utcNowProvider;

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

    [ObservableProperty]
    private bool _isPasswordVisible;

    public string PasswordVisibilityActionText =>
        IsPasswordVisible ? "Hide password" : "Show password";

    public LoginViewModel(
        AuthenticationService authenticationService,
        NavigationService navigationService,
        Func<DateTimeOffset> utcNowProvider)
    {
        ArgumentNullException.ThrowIfNull(authenticationService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(utcNowProvider);

        _authenticationService = authenticationService;
        _navigationService = navigationService;
        _utcNowProvider = utcNowProvider;

        _navigationService.NavigationChanged += OnNavigationChanged;

        if (_navigationService.CurrentRoute == AppRoute.Login)
        {
            StatusMessage = _navigationService.CurrentStatusMessage ?? string.Empty;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoginAsync()
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
            var nowUtc = _utcNowProvider();
            var result = await _authenticationService
                .LoginAsync(Username, Password, nowUtc);

            Password = string.Empty;
            IsPasswordVisible = false;

            if (result.IsSuccess)
            {
                _navigationService.Navigate(AppRoute.Dashboard);
                return;
            }

            ErrorMessage = result.ErrorMessage ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            Password = string.Empty;
            IsPasswordVisible = false;
            ErrorMessage = TechnicalFailureMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoToRegister()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        IsPasswordVisible = false;
        _navigationService.Navigate(AppRoute.Register);
    }

    partial void OnIsPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(PasswordVisibilityActionText));
    }

    private void OnNavigationChanged(object? sender, EventArgs eventArgs)
    {
        if (_navigationService.CurrentRoute == AppRoute.Login)
        {
            StatusMessage = _navigationService.CurrentStatusMessage ?? string.Empty;
        }
    }
}
