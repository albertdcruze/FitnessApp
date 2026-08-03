using System;
using System.Threading.Tasks;
using FitnessApp.Common;
using FitnessApp.Models;
using FitnessApp.Repositories;
using FitnessApp.Services;
using FitnessApp.Tests.Data;
using Xunit;

namespace FitnessApp.Tests.Services;

public sealed class AuthenticationServiceTests
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 8, 3, 8, 0, 0, TimeSpan.Zero);

    private const string Username = "Oak01";
    private const string Password = "FitnessPass1";
    private const string WrongPassword = "WrongPass1!A";

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateUsername_RejectsMissingValues(string username)
    {
        var service = CreateValidationService();

        var result = service.ValidateUsername(username);

        Assert.False(result.IsSuccess);
        Assert.Equal("Username is required.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateUsername_RejectsNull()
    {
        var service = CreateValidationService();

        var result = service.ValidateUsername(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal("Username is required.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateUsername_ReturnsTheOriginalAlphanumericValue()
    {
        var service = CreateValidationService();

        var result = service.ValidateUsername("Oak123");

        Assert.True(result.IsSuccess);
        Assert.Equal("Oak123", result.Value);
    }

    [Theory]
    [InlineData("Oak User")]
    [InlineData(" Oak123")]
    [InlineData("Oak123 ")]
    [InlineData("Oak_User")]
    [InlineData("Oak-User")]
    [InlineData("Oak@123")]
    public void ValidateUsername_RejectsSpacesAndSymbols(string username)
    {
        var service = CreateValidationService();

        var result = service.ValidateUsername(username);

        Assert.False(result.IsSuccess);
        Assert.Equal("Username can contain letters and numbers only.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateUsername_AcceptsThirtyCharactersAndRejectsThirtyOne()
    {
        var service = CreateValidationService();
        var thirtyCharacters = new string('A', 30);
        var thirtyOneCharacters = new string('A', 31);

        var validResult = service.ValidateUsername(thirtyCharacters);
        var invalidResult = service.ValidateUsername(thirtyOneCharacters);

        Assert.True(validResult.IsSuccess);
        Assert.Equal(thirtyCharacters, validResult.Value);
        Assert.False(invalidResult.IsSuccess);
        Assert.Equal(
            "Username must be between 1 and 30 characters.",
            invalidResult.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    public void ValidatePassword_RejectsMissingValues(string password)
    {
        var service = CreateValidationService();

        var result = service.ValidatePassword(password);

        Assert.False(result.IsSuccess);
        Assert.Equal("Password is required.", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePassword_RejectsNull()
    {
        var service = CreateValidationService();

        var result = service.ValidatePassword(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal("Password is required.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("FitnessPas1")]
    [InlineData("FitnessPass12")]
    public void ValidatePassword_RequiresExactlyTwelveCharacters(string password)
    {
        var service = CreateValidationService();

        var result = service.ValidatePassword(password);

        Assert.False(result.IsSuccess);
        Assert.Equal("Password must be exactly 12 characters.", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePassword_RequiresAnUppercaseLetter()
    {
        var service = CreateValidationService();

        var result = service.ValidatePassword("abcdefghijkl");

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Password must contain at least one uppercase letter.",
            result.ErrorMessage);
    }

    [Fact]
    public void ValidatePassword_RequiresALowercaseLetter()
    {
        var service = CreateValidationService();

        var result = service.ValidatePassword("ABCDEFGHIJKL");

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Password must contain at least one lowercase letter.",
            result.ErrorMessage);
    }

    [Theory]
    [InlineData("FitnessPass1")]
    [InlineData("SecurePass!1")]
    public void ValidatePassword_AllowsDigitsAndSymbols(string password)
    {
        var service = CreateValidationService();

        var result = service.ValidatePassword(password);

        Assert.True(result.IsSuccess);
        Assert.Equal(password, result.Value);
    }

    [Fact]
    public async Task RegisterAsync_PersistsAUserWithoutStartingASession()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);

        var result = await service.RegisterAsync(Username, Password);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.UserId > 0);
        Assert.Equal(Username, result.Value.Username);
        Assert.Equal(0, result.Value.FailedLoginAttempts);
        Assert.Null(result.Value.LockoutUntilUtc);
        Assert.Null(service.CurrentUser);

        var persistedUser = await database.Users.FindByUsernameAsync(Username);
        Assert.NotNull(persistedUser);
        Assert.Equal(result.Value.UserId, persistedUser!.UserId);
    }

    [Fact]
    public async Task RegisterAsync_StoresThePasswordInTheApprovedHashFormat()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);

        await service.RegisterAsync(Username, Password);
        var persistedUser = await database.Users.FindByUsernameAsync(Username);

        Assert.NotNull(persistedUser);
        Assert.NotEqual(Password, persistedUser!.PasswordHash);
        Assert.DoesNotContain(Password, persistedUser.PasswordHash);

        var segments = persistedUser.PasswordHash.Split('$');
        Assert.Equal(5, segments.Length);
        Assert.Equal("v1", segments[0]);
        Assert.Equal("PBKDF2-SHA256", segments[1]);
        Assert.Equal("100000", segments[2]);
        Assert.Equal(16, Convert.FromBase64String(segments[3]).Length);
        Assert.Equal(32, Convert.FromBase64String(segments[4]).Length);
    }

    [Fact]
    public async Task RegisterAsync_RejectsCaseInsensitiveDuplicateUsername()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);

        var firstResult = await service.RegisterAsync("Oak01", Password);
        var duplicateResult = await service.RegisterAsync("oak01", Password);

        Assert.True(firstResult.IsSuccess);
        Assert.False(duplicateResult.IsSuccess);
        Assert.Equal("Username already exists.", duplicateResult.ErrorMessage);
        Assert.Equal(1, await CountUsersAsync(database));
    }

    [Fact]
    public async Task RegisterAsync_DoesNotInsertAnInvalidUsername()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);

        var result = await service.RegisterAsync("Oak_User", Password);

        Assert.False(result.IsSuccess);
        Assert.Null(await database.Users.FindByUsernameAsync("Oak_User"));
        Assert.Equal(0, await CountUsersAsync(database));
    }

    [Fact]
    public async Task RegisterAsync_DoesNotInsertAnInvalidPassword()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);

        var result = await service.RegisterAsync(Username, "short");

        Assert.False(result.IsSuccess);
        Assert.Null(await database.Users.FindByUsernameAsync(Username));
        Assert.Equal(0, await CountUsersAsync(database));
    }

    [Fact]
    public async Task LoginAsync_SucceedsAndSetsCurrentUser()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        var registration = await service.RegisterAsync(Username, Password);

        var result = await service.LoginAsync("oak01", Password, BaseTime);

        Assert.True(registration.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(registration.Value!.UserId, result.Value!.UserId);
        Assert.Equal(result.Value.UserId, service.CurrentUser!.UserId);
    }

    [Fact]
    public async Task LoginAsync_UsesTheSameGenericMessageForWrongAndUnknownCredentials()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        await service.RegisterAsync(Username, Password);

        var wrongPasswordResult = await service.LoginAsync(Username, WrongPassword, BaseTime);
        var unknownUsernameResult = await service.LoginAsync("Missing01", Password, BaseTime);

        Assert.False(wrongPasswordResult.IsSuccess);
        Assert.False(unknownUsernameResult.IsSuccess);
        Assert.Equal(wrongPasswordResult.ErrorMessage, unknownUsernameResult.ErrorMessage);
        Assert.Equal("Username or password is incorrect.", wrongPasswordResult.ErrorMessage);
        Assert.Equal(1, await CountUsersAsync(database));
        Assert.Null(service.CurrentUser);
    }

    [Fact]
    public async Task LoginAsync_IncorrectPasswordPersistsOneFailedAttempt()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        await service.RegisterAsync(Username, Password);

        var result = await service.LoginAsync(Username, WrongPassword, BaseTime);
        var persistedUser = await database.Users.FindByUsernameAsync(Username);

        Assert.False(result.IsSuccess);
        Assert.Equal("Username or password is incorrect.", result.ErrorMessage);
        Assert.NotNull(persistedUser);
        Assert.Equal(1, persistedUser!.FailedLoginAttempts);
        Assert.Null(persistedUser.LockoutUntilUtc);
        Assert.Null(service.CurrentUser);
    }

    [Fact]
    public async Task Logout_ClearsCurrentUserOnly()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        await service.RegisterAsync(Username, Password);
        await service.LoginAsync(Username, Password, BaseTime);

        service.Logout();

        Assert.Null(service.CurrentUser);
        var persistedUser = await database.Users.FindByUsernameAsync(Username);
        Assert.NotNull(persistedUser);
        Assert.Equal(0, persistedUser!.FailedLoginAttempts);
        Assert.Null(persistedUser.LockoutUntilUtc);
    }

    [Fact]
    public async Task LoginAsync_PersistsAttemptsOneThroughFourWithoutLockout()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        await service.RegisterAsync(Username, Password);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var result = await service.LoginAsync(Username, WrongPassword, BaseTime.AddMinutes(attempt));
            var persistedUser = await database.Users.FindByUsernameAsync(Username);

            Assert.False(result.IsSuccess);
            Assert.Equal("Username or password is incorrect.", result.ErrorMessage);
            Assert.NotNull(persistedUser);
            Assert.Equal(attempt, persistedUser!.FailedLoginAttempts);
            Assert.Null(persistedUser.LockoutUntilUtc);
        }
    }

    [Fact]
    public async Task LoginAsync_FifthFailureCreatesFiveMinuteLockout()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        await service.RegisterAsync(Username, Password);

        OperationResult<User>? fifthResult = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            fifthResult = await service.LoginAsync(Username, WrongPassword, BaseTime);
        }

        var persistedUser = await database.Users.FindByUsernameAsync(Username);

        Assert.NotNull(fifthResult);
        Assert.False(fifthResult!.IsSuccess);
        Assert.Equal("Username or password is incorrect.", fifthResult.ErrorMessage);
        Assert.NotNull(persistedUser);
        Assert.Equal(5, persistedUser!.FailedLoginAttempts);
        Assert.Equal(BaseTime.AddMinutes(5), persistedUser.LockoutUntilUtc);
    }

    [Fact]
    public async Task LoginAsync_ActiveLockoutDoesNotChangePersistedState()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        var lockedUser = await TriggerLockoutAsync(service, database);
        var lockoutUntilUtc = lockedUser.LockoutUntilUtc!.Value;

        var result = await service.LoginAsync(Username, Password, BaseTime.AddMinutes(1));
        var persistedUser = await database.Users.FindByUsernameAsync(Username);

        Assert.False(result.IsSuccess);
        Assert.Equal("Too many failed login attempts. Try again later.", result.ErrorMessage);
        Assert.Null(service.CurrentUser);
        Assert.NotNull(persistedUser);
        Assert.Equal(5, persistedUser!.FailedLoginAttempts);
        Assert.Equal(lockoutUntilUtc, persistedUser.LockoutUntilUtc);
    }

    [Fact]
    public async Task LoginAsync_OneInstantBeforeExpiryRemainsBlocked()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        var lockedUser = await TriggerLockoutAsync(service, database);
        var lockoutUntilUtc = lockedUser.LockoutUntilUtc!.Value;

        var result = await service.LoginAsync(
            Username,
            Password,
            lockoutUntilUtc.AddSeconds(-1));
        var persistedUser = await database.Users.FindByUsernameAsync(Username);

        Assert.False(result.IsSuccess);
        Assert.Equal("Too many failed login attempts. Try again later.", result.ErrorMessage);
        Assert.NotNull(persistedUser);
        Assert.Equal(5, persistedUser!.FailedLoginAttempts);
        Assert.Equal(lockoutUntilUtc, persistedUser.LockoutUntilUtc);
    }

    [Fact]
    public async Task LoginAsync_AtExpiryAllowsCorrectPasswordAndResetsState()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        var lockedUser = await TriggerLockoutAsync(service, database);
        var lockoutUntilUtc = lockedUser.LockoutUntilUtc!.Value;

        var result = await service.LoginAsync(Username, Password, lockoutUntilUtc);
        var persistedUser = await database.Users.FindByUsernameAsync(Username);

        Assert.True(result.IsSuccess);
        Assert.NotNull(service.CurrentUser);
        Assert.NotNull(persistedUser);
        Assert.Equal(0, persistedUser!.FailedLoginAttempts);
        Assert.Null(persistedUser.LockoutUntilUtc);
    }

    [Fact]
    public async Task LoginAsync_AfterExpiryAllowsCorrectPassword()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        var lockedUser = await TriggerLockoutAsync(service, database);

        var result = await service.LoginAsync(
            Username,
            Password,
            lockedUser.LockoutUntilUtc!.Value.AddSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.NotNull(service.CurrentUser);
    }

    [Fact]
    public async Task LoginAsync_WrongPasswordAfterExpiryStartsAtAttemptOne()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        var lockedUser = await TriggerLockoutAsync(service, database);

        var result = await service.LoginAsync(
            Username,
            WrongPassword,
            lockedUser.LockoutUntilUtc!.Value.AddSeconds(1));
        var persistedUser = await database.Users.FindByUsernameAsync(Username);

        Assert.False(result.IsSuccess);
        Assert.Equal("Username or password is incorrect.", result.ErrorMessage);
        Assert.NotNull(persistedUser);
        Assert.Equal(1, persistedUser!.FailedLoginAttempts);
        Assert.Null(persistedUser.LockoutUntilUtc);
    }

    [Fact]
    public async Task LoginAsync_SuccessfulLoginResetsEarlierFailures()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);
        var registration = await service.RegisterAsync(Username, Password);
        var originalUser = registration.Value!;
        await service.LoginAsync(Username, WrongPassword, BaseTime);
        await service.LoginAsync(Username, WrongPassword, BaseTime.AddSeconds(1));

        var result = await service.LoginAsync(Username, Password, BaseTime.AddSeconds(2));
        var persistedUser = await database.Users.FindByUsernameAsync(Username);

        Assert.True(result.IsSuccess);
        Assert.NotNull(persistedUser);
        Assert.Equal(0, persistedUser!.FailedLoginAttempts);
        Assert.Null(persistedUser.LockoutUntilUtc);
        Assert.Equal(originalUser.Username, persistedUser.Username);
        Assert.Equal(originalUser.PasswordHash, persistedUser.PasswordHash);
        Assert.Equal(originalUser.CreatedAtUtc, persistedUser.CreatedAtUtc);
        Assert.Equal(persistedUser.UserId, service.CurrentUser!.UserId);
    }

    [Fact]
    public async Task LockoutPersistsWhenTheServiceIsReconstructed()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var serviceA = new AuthenticationService(database.Users);
        var lockedUser = await TriggerLockoutAsync(serviceA, database);
        var serviceB = new AuthenticationService(new UserRepository(database.ConnectionString));

        var blockedResult = await serviceB.LoginAsync(Username, Password, BaseTime.AddMinutes(1));
        var successResult = await serviceB.LoginAsync(
            Username,
            Password,
            lockedUser.LockoutUntilUtc!.Value);

        Assert.False(blockedResult.IsSuccess);
        Assert.Equal("Too many failed login attempts. Try again later.", blockedResult.ErrorMessage);
        Assert.True(successResult.IsSuccess);
        Assert.NotNull(serviceB.CurrentUser);
    }

    [Fact]
    public async Task RegisterAsync_UsesDifferentSaltsForTheSamePassword()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new AuthenticationService(database.Users);

        await service.RegisterAsync("Oak01", Password);
        await service.RegisterAsync("Oak02", Password);
        var firstUser = await database.Users.FindByUsernameAsync("Oak01");
        var secondUser = await database.Users.FindByUsernameAsync("Oak02");

        Assert.NotNull(firstUser);
        Assert.NotNull(secondUser);
        Assert.NotEqual(firstUser!.PasswordHash, secondUser!.PasswordHash);
    }

    [Fact]
    public async Task LoginAsync_MalformedStoredHashIsANormalFailedAttempt()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        await database.Users.AddAsync(new User(
            "Corrupt01",
            "not-a-valid-password-hash",
            BaseTime));
        var service = new AuthenticationService(database.Users);

        var result = await service.LoginAsync("Corrupt01", Password, BaseTime);
        var persistedUser = await database.Users.FindByUsernameAsync("Corrupt01");

        Assert.False(result.IsSuccess);
        Assert.Equal("Username or password is incorrect.", result.ErrorMessage);
        Assert.NotNull(persistedUser);
        Assert.Equal(1, persistedUser!.FailedLoginAttempts);
        Assert.Null(service.CurrentUser);
    }

    private static AuthenticationService CreateValidationService()
    {
        return new AuthenticationService(new UserRepository("Data Source=:memory:"));
    }

    private static async Task<User> TriggerLockoutAsync(
        AuthenticationService service,
        RepositoryTestDatabase database)
    {
        var registration = await service.RegisterAsync(Username, Password);
        Assert.True(registration.IsSuccess);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await service.LoginAsync(Username, WrongPassword, BaseTime);
        }

        var lockedUser = await database.Users.FindByUsernameAsync(Username);
        Assert.NotNull(lockedUser);
        return lockedUser!;
    }

    private static async Task<long> CountUsersAsync(RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users;";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
