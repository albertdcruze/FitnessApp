using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FitTrack.Common;
using FitTrack.Repositories;
using FitTrack.Services;
using FitTrack.Tests.Data;
using FitTrack.ViewModels;
using Xunit;

namespace FitTrack.Tests.ViewModels;

public sealed class LoginViewModelTests
{
    private const string Password = "FitnessPass1";
    private const string WrongPassword = "WrongPass123";
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var authenticationService = new AuthenticationService(
            new UserRepository("Data Source=:memory:"));
        var navigationService = new NavigationService();

        Assert.Throws<ArgumentNullException>(() =>
            new LoginViewModel(null!, navigationService, static () => FixedUtcNow));
        Assert.Throws<ArgumentNullException>(() =>
            new LoginViewModel(authenticationService, null!, static () => FixedUtcNow));
        Assert.Throws<ArgumentNullException>(() =>
            new LoginViewModel(authenticationService, navigationService, null!));
    }

    [Fact]
    public void PasswordVisibilityState_UpdatesTheAccessibleActionText()
    {
        var authenticationService = new AuthenticationService(
            new UserRepository("Data Source=:memory:"));
        var navigationService = new NavigationService();
        var viewModel = CreateViewModel(authenticationService, navigationService);

        Assert.False(viewModel.IsPasswordVisible);
        Assert.Equal("Show password", viewModel.PasswordVisibilityActionText);

        viewModel.IsPasswordVisible = true;

        Assert.True(viewModel.IsPasswordVisible);
        Assert.Equal("Hide password", viewModel.PasswordVisibilityActionText);
    }

    [Fact]
    public async Task LoginCommand_SucceedsWithTheSharedAuthenticationSession()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        var registration = await authenticationService.RegisterAsync("LoginUser01", Password);
        var navigationService = new NavigationService();
        var viewModel = CreateViewModel(authenticationService, navigationService);
        viewModel.Username = "loginuser01";
        viewModel.Password = Password;

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.True(registration.IsSuccess);
        Assert.Equal(AppRoute.Dashboard, navigationService.CurrentRoute);
        Assert.NotNull(authenticationService.CurrentUser);
        Assert.Equal(registration.Value!.UserId, authenticationService.CurrentUser!.UserId);
        Assert.Equal("loginuser01", viewModel.Username);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.False(viewModel.IsPasswordVisible);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task LoginCommand_RemembersCredentialsAndRestoresThemWhenLoginRouteIsOpened()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "FitTrackTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        try
        {
            await using var database = await RepositoryTestDatabase.CreateAsync();
            var authenticationService = new AuthenticationService(database.Users);
            await authenticationService.RegisterAsync("RememberedUser01", Password);
            var store = new RememberedCredentialsStore(
                Path.Combine(directoryPath, "remembered-credentials.dat"));
            var navigationService = new NavigationService();
            var viewModel = CreateViewModel(
                authenticationService,
                navigationService,
                FixedUtcNow,
                store);
            viewModel.Username = "RememberedUser01";
            viewModel.Password = Password;
            viewModel.RememberMe = true;

            await viewModel.LoginCommand.ExecuteAsync(null);

            Assert.Equal(AppRoute.Dashboard, navigationService.CurrentRoute);
            Assert.Equal(string.Empty, viewModel.Password);
            authenticationService.Logout();
            navigationService.Navigate(AppRoute.Login);

            Assert.Equal("RememberedUser01", viewModel.Username);
            Assert.Equal(Password, viewModel.Password);
            Assert.True(viewModel.RememberMe);
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoginCommand_WhenRememberMeIsDisabled_ClearsExistingCredentials()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "FitTrackTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        try
        {
            await using var database = await RepositoryTestDatabase.CreateAsync();
            var authenticationService = new AuthenticationService(database.Users);
            await authenticationService.RegisterAsync("UnrememberedUser01", Password);
            var store = new RememberedCredentialsStore(
                Path.Combine(directoryPath, "remembered-credentials.dat"));
            store.Save("PreviousUser01", "PreviousPass1");
            var navigationService = new NavigationService();
            var viewModel = CreateViewModel(
                authenticationService,
                navigationService,
                FixedUtcNow,
                store);
            viewModel.Username = "UnrememberedUser01";
            viewModel.Password = Password;
            viewModel.RememberMe = false;

            await viewModel.LoginCommand.ExecuteAsync(null);

            Assert.Equal(AppRoute.Dashboard, navigationService.CurrentRoute);
            Assert.Null(store.Load());
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoginCommand_UsesTheSameGenericMessageForWrongAndUnknownCredentials()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        await authenticationService.RegisterAsync("KnownUser01", Password);
        var navigationService = new NavigationService();
        var viewModel = CreateViewModel(authenticationService, navigationService);

        viewModel.Username = "KnownUser01";
        viewModel.Password = WrongPassword;
        await viewModel.LoginCommand.ExecuteAsync(null);
        var wrongPasswordMessage = viewModel.ErrorMessage;

        viewModel.Username = "MissingUser01";
        viewModel.Password = Password;
        await viewModel.LoginCommand.ExecuteAsync(null);
        var unknownUsernameMessage = viewModel.ErrorMessage;

        Assert.Equal(AppRoute.Login, navigationService.CurrentRoute);
        Assert.Equal("Username or password is incorrect.", wrongPasswordMessage);
        Assert.Equal(wrongPasswordMessage, unknownUsernameMessage);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.Null(authenticationService.CurrentUser);
    }

    [Fact]
    public async Task LoginCommand_ExposesAnActiveLockoutAndClearsThePassword()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        await authenticationService.RegisterAsync("LockedUser01", Password);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await authenticationService.LoginAsync("LockedUser01", WrongPassword, FixedUtcNow);
        }

        var navigationService = new NavigationService();
        var viewModel = CreateViewModel(authenticationService, navigationService, FixedUtcNow.AddMinutes(1));
        viewModel.Username = "LockedUser01";
        viewModel.Password = Password;

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal(AppRoute.Login, navigationService.CurrentRoute);
        Assert.Equal(
            "Too many failed login attempts. Try again later.",
            viewModel.ErrorMessage);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.Null(authenticationService.CurrentUser);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task LoginCommand_UsesTheSuppliedUtcClockForLockoutExpiry()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        await authenticationService.RegisterAsync("ClockUser01", Password);
        var navigationService = new NavigationService();
        var viewModel = CreateViewModel(authenticationService, navigationService, FixedUtcNow);
        viewModel.Username = "ClockUser01";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            viewModel.Password = WrongPassword;
            await viewModel.LoginCommand.ExecuteAsync(null);
        }

        var persistedUser = await database.Users.FindByUsernameAsync("ClockUser01");

        Assert.NotNull(persistedUser);
        Assert.Equal(FixedUtcNow.AddMinutes(5), persistedUser!.LockoutUntilUtc);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.Equal(AppRoute.Login, navigationService.CurrentRoute);
        Assert.Null(authenticationService.CurrentUser);
    }

    [Fact]
    public async Task LoginCommand_HandlesDatabaseFailuresWithASafeMessage()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var authenticationService = new AuthenticationService(database.Users);
        var navigationService = new NavigationService();
        var viewModel = CreateViewModel(authenticationService, navigationService);
        await DropUsersTableAsync(database);
        viewModel.Username = "Technical01";
        viewModel.Password = Password;

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal(AppRoute.Login, navigationService.CurrentRoute);
        Assert.Equal("Unable to sign in right now. Please try again.", viewModel.ErrorMessage);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.Equal("Technical01", viewModel.Username);
        Assert.DoesNotContain("SQLite", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(database.ConnectionString, viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void GoToRegisterCommand_ClearsMessagesAndNavigates()
    {
        var authenticationService = new AuthenticationService(
            new UserRepository("Data Source=:memory:"));
        var navigationService = new NavigationService();
        var viewModel = CreateViewModel(authenticationService, navigationService);
        viewModel.ErrorMessage = "Old error";
        viewModel.StatusMessage = "Old status";
        viewModel.IsPasswordVisible = true;

        viewModel.GoToRegisterCommand.Execute(null);

        Assert.Equal(AppRoute.Register, navigationService.CurrentRoute);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
        Assert.Equal(string.Empty, viewModel.StatusMessage);
        Assert.False(viewModel.IsPasswordVisible);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task LoginCommand_IsAnAsyncCommandAndRestoresBusyState()
    {
        var authenticationService = new AuthenticationService(
            new UserRepository("Data Source=:memory:"));
        var navigationService = new NavigationService();
        var viewModel = CreateViewModel(authenticationService, navigationService);
        viewModel.Username = string.Empty;
        viewModel.Password = Password;

        Assert.IsAssignableFrom<IAsyncRelayCommand>(viewModel.LoginCommand);

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsBusy);
        Assert.Equal("Username or password is incorrect.", viewModel.ErrorMessage);
        Assert.Equal(string.Empty, viewModel.Password);
    }

    private static LoginViewModel CreateViewModel(
        AuthenticationService authenticationService,
        NavigationService navigationService,
        DateTimeOffset? utcNow = null,
        RememberedCredentialsStore? rememberedCredentialsStore = null)
    {
        return new LoginViewModel(
            authenticationService,
            navigationService,
            () => utcNow ?? FixedUtcNow,
            rememberedCredentialsStore);
    }

    private static async Task DropUsersTableAsync(RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE Users;";
        await command.ExecuteNonQueryAsync();
    }
}
