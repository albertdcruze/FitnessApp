using System;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FitnessApp.Common;
using FitnessApp.Repositories;
using FitnessApp.Services;
using FitnessApp.Tests.Data;
using FitnessApp.ViewModels;
using Xunit;

namespace FitnessApp.Tests.ViewModels;

public sealed class RegisterViewModelTests
{
    private const string Password = "FitnessPass1";

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var authenticationService = new AuthenticationService(
            new UserRepository("Data Source=:memory:"));
        var navigationService = new NavigationService();

        Assert.Throws<ArgumentNullException>(() =>
            new RegisterViewModel(null!, navigationService));
        Assert.Throws<ArgumentNullException>(() =>
            new RegisterViewModel(authenticationService, null!));
    }

    [Fact]
    public async Task RegisterCommand_CreatesAUserAndSendsSuccessFeedbackToLogin()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        var navigationService = new NavigationService();
        var loginViewModel = new LoginViewModel(
            authenticationService,
            navigationService,
            static () => DateTimeOffset.UtcNow);
        var registerViewModel = new RegisterViewModel(
            authenticationService,
            navigationService);
        navigationService.Navigate(AppRoute.Register);
        registerViewModel.Username = "RegisterUser01";
        registerViewModel.Password = Password;

        await registerViewModel.RegisterCommand.ExecuteAsync(null);

        var persistedUser = await database.Users.FindByUsernameAsync("RegisterUser01");

        Assert.NotNull(persistedUser);
        Assert.Equal(AppRoute.Login, navigationService.CurrentRoute);
        Assert.Equal(string.Empty, registerViewModel.Password);
        Assert.Equal(
            "Registration successful. You can now sign in.",
            registerViewModel.StatusMessage);
        Assert.Equal(
            "Registration successful. You can now sign in.",
            loginViewModel.StatusMessage);
        Assert.Equal(
            "Registration successful. You can now sign in.",
            navigationService.CurrentStatusMessage);
        Assert.Null(authenticationService.CurrentUser);
        Assert.False(registerViewModel.IsBusy);
    }

    [Fact]
    public async Task RegisterCommand_ExposesDuplicateUsernameWithoutAuthenticating()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        await authenticationService.RegisterAsync("DuplicateUser01", Password);
        var navigationService = new NavigationService();
        var viewModel = new RegisterViewModel(authenticationService, navigationService);
        navigationService.Navigate(AppRoute.Register);
        viewModel.Username = "duplicateuser01";
        viewModel.Password = Password;

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(AppRoute.Register, navigationService.CurrentRoute);
        Assert.Equal("Username already exists.", viewModel.ErrorMessage);
        Assert.Equal("duplicateuser01", viewModel.Username);
        Assert.Equal(Password, viewModel.Password);
        Assert.Equal(1, await CountUsersAsync(database));
        Assert.Null(authenticationService.CurrentUser);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RegisterCommand_RetainsInputsForAControlledUsernameFailure()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        var navigationService = new NavigationService();
        var viewModel = new RegisterViewModel(authenticationService, navigationService);
        navigationService.Navigate(AppRoute.Register);
        viewModel.Username = "Invalid_User";
        viewModel.Password = Password;

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(AppRoute.Register, navigationService.CurrentRoute);
        Assert.Equal("Username can contain letters and numbers only.", viewModel.ErrorMessage);
        Assert.Equal("Invalid_User", viewModel.Username);
        Assert.Equal(Password, viewModel.Password);
        Assert.Equal(0, await CountUsersAsync(database));
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RegisterCommand_ExposesAControlledPasswordFailureWithoutWriting()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        var navigationService = new NavigationService();
        var viewModel = new RegisterViewModel(authenticationService, navigationService);
        navigationService.Navigate(AppRoute.Register);
        viewModel.Username = "PasswordUser01";
        viewModel.Password = "short";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(AppRoute.Register, navigationService.CurrentRoute);
        Assert.Equal("Password must be exactly 12 characters.", viewModel.ErrorMessage);
        Assert.Equal("PasswordUser01", viewModel.Username);
        Assert.Equal("short", viewModel.Password);
        Assert.Equal(0, await CountUsersAsync(database));
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RegisterCommand_HandlesDatabaseFailuresWithASafeMessage()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        var navigationService = new NavigationService();
        var viewModel = new RegisterViewModel(authenticationService, navigationService);
        navigationService.Navigate(AppRoute.Register);
        await DropUsersTableAsync(database);
        viewModel.Username = "TechnicalUser01";
        viewModel.Password = Password;

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(AppRoute.Register, navigationService.CurrentRoute);
        Assert.Equal(
            "Unable to create the account right now. Please try again.",
            viewModel.ErrorMessage);
        Assert.Equal("TechnicalUser01", viewModel.Username);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.DoesNotContain("SQLite", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(database.ConnectionString, viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void BackToLoginCommand_ClearsMessagesAndDoesNotAuthenticate()
    {
        var authenticationService = new AuthenticationService(
            new UserRepository("Data Source=:memory:"));
        var navigationService = new NavigationService();
        var viewModel = new RegisterViewModel(authenticationService, navigationService);
        navigationService.Navigate(AppRoute.Register);
        viewModel.ErrorMessage = "Old error";
        viewModel.StatusMessage = "Old status";

        viewModel.BackToLoginCommand.Execute(null);

        Assert.Equal(AppRoute.Login, navigationService.CurrentRoute);
        Assert.Null(navigationService.CurrentStatusMessage);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
        Assert.Equal(string.Empty, viewModel.StatusMessage);
        Assert.Null(authenticationService.CurrentUser);
    }

    [Fact]
    public async Task RegisterCommand_PreventsDuplicateExecutionWhileBusy()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        var navigationService = new NavigationService();
        var viewModel = new RegisterViewModel(authenticationService, navigationService);
        var registrationReachedInsert = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRegistration = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        authenticationService.BeforeRegistrationInsertAsync = async () =>
        {
            registrationReachedInsert.TrySetResult(true);
            await releaseRegistration.Task.ConfigureAwait(false);
        };
        navigationService.Navigate(AppRoute.Register);
        viewModel.Username = "ConcurrentUser01";
        viewModel.Password = Password;

        Assert.IsAssignableFrom<IAsyncRelayCommand>(viewModel.RegisterCommand);
        var firstExecution = viewModel.RegisterCommand.ExecuteAsync(null);
        await registrationReachedInsert.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(viewModel.IsBusy);

        var secondExecution = viewModel.RegisterCommand.ExecuteAsync(null);
        releaseRegistration.TrySetResult(true);
        await Task.WhenAll(firstExecution, secondExecution);

        Assert.Equal(1, await CountUsersAsync(database));
        Assert.False(viewModel.IsBusy);
        Assert.Equal(AppRoute.Login, navigationService.CurrentRoute);
    }

    private static async Task<long> CountUsersAsync(RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users;";
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture);
    }

    private static async Task DropUsersTableAsync(RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE Users;";
        await command.ExecuteNonQueryAsync();
    }
}
