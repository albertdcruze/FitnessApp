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
    private readonly RememberedCredentialsStore? _rememberedCredentialsStore;
    private AppRoute _lastObservedRoute;

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

    [ObservableProperty]
    private bool _rememberMe;

    public string PasswordVisibilityActionText =>
        IsPasswordVisible ? "Hide password" : "Show password";

    public LoginViewModel(
        AuthenticationService authenticationService,
        NavigationService navigationService,
        Func<DateTimeOffset> utcNowProvider)
        : this(authenticationService, navigationService, utcNowProvider, null)
    {
    }

    public LoginViewModel(
        AuthenticationService authenticationService,
        NavigationService navigationService,
        Func<DateTimeOffset> utcNowProvider,
        RememberedCredentialsStore? rememberedCredentialsStore)
    {
        ArgumentNullException.ThrowIfNull(authenticationService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(utcNowProvider);

        _authenticationService = authenticationService;
        _navigationService = navigationService;
        _utcNowProvider = utcNowProvider;
        _rememberedCredentialsStore = rememberedCredentialsStore;
        _lastObservedRoute = _navigationService.CurrentRoute;

        _navigationService.NavigationChanged += OnNavigationChanged;

        if (_navigationService.CurrentRoute == AppRoute.Login)
        {
            LoadRememberedCredentials();
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

            if (result.IsSuccess)
            {
                SaveRememberedCredentials(result.Value?.Username ?? Username, Password);
                Password = string.Empty;
                IsPasswordVisible = false;
                _navigationService.Navigate(AppRoute.Dashboard);
                return;
            }

            Password = string.Empty;
            IsPasswordVisible = false;
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

    partial void OnRememberMeChanged(bool value)
    {
        if (!value)
        {
            _rememberedCredentialsStore?.Clear();
        }
    }

    partial void OnIsPasswordVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(PasswordVisibilityActionText));
    }

    private void OnNavigationChanged(object? sender, EventArgs eventArgs)
    {
        var currentRoute = _navigationService.CurrentRoute;
        var enteredLogin = currentRoute == AppRoute.Login
                           && _lastObservedRoute != AppRoute.Login;
        _lastObservedRoute = currentRoute;

        if (currentRoute == AppRoute.Login)
        {
            if (enteredLogin)
            {
                LoadRememberedCredentials();
            }

            StatusMessage = _navigationService.CurrentStatusMessage ?? string.Empty;
        }
    }

    private void LoadRememberedCredentials()
    {
        var rememberedCredentials = _rememberedCredentialsStore?.Load();
        if (rememberedCredentials is null)
        {
            return;
        }

        Username = rememberedCredentials.Username;
        Password = rememberedCredentials.Password;
        RememberMe = true;
    }

    private void SaveRememberedCredentials(string username, string password)
    {
        if (_rememberedCredentialsStore is null)
        {
            return;
        }

        if (RememberMe)
        {
            _rememberedCredentialsStore.Save(username, password);
        }
        else
        {
            _rememberedCredentialsStore.Clear();
        }
    }
}
