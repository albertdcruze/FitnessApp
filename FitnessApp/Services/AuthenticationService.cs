using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FitnessApp.Common;
using FitnessApp.Models;
using FitnessApp.Repositories;
using Microsoft.Data.Sqlite;

namespace FitnessApp.Services;

public sealed class AuthenticationService
{
    private const int MaximumFailedLoginAttempts = 5;
    private const int PasswordHashIterations = 100000;
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int SqliteConstraintErrorCode = 19;
    private const int SqliteConstraintUniqueErrorCode = 2067;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private const string DuplicateUsernameMessage = "Username already exists.";
    private const string InvalidCredentialsMessage = "Username or password is incorrect.";
    private const string ActiveLockoutMessage = "Too many failed login attempts. Try again later.";

    private readonly UserRepository _userRepository;

    public AuthenticationService(UserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public User? CurrentUser { get; private set; }

    internal Func<Task>? BeforeRegistrationInsertAsync { get; set; }

    public OperationResult<string> ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return OperationResult<string>.Failure("Username is required.");
        }

        if (username.Length is < 1 or > 30)
        {
            return OperationResult<string>.Failure(
                "Username must be between 1 and 30 characters.");
        }

        foreach (var character in username)
        {
            var isAsciiLetter = character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
            var isAsciiDigit = character is >= '0' and <= '9';
            if (!isAsciiLetter && !isAsciiDigit)
            {
                return OperationResult<string>.Failure(
                    "Username can contain letters and numbers only.");
            }
        }

        return OperationResult<string>.Success(username);
    }

    public OperationResult<string> ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return OperationResult<string>.Failure("Password is required.");
        }

        if (password.Length != 12)
        {
            return OperationResult<string>.Failure(
                "Password must be exactly 12 characters.");
        }

        var containsUppercase = false;
        var containsLowercase = false;

        foreach (var character in password)
        {
            if (character is >= 'A' and <= 'Z')
            {
                containsUppercase = true;
            }
            else if (character is >= 'a' and <= 'z')
            {
                containsLowercase = true;
            }
        }

        if (!containsUppercase)
        {
            return OperationResult<string>.Failure(
                "Password must contain at least one uppercase letter.");
        }

        if (!containsLowercase)
        {
            return OperationResult<string>.Failure(
                "Password must contain at least one lowercase letter.");
        }

        return OperationResult<string>.Success(password);
    }

    public async Task<OperationResult<User>> RegisterAsync(string username, string password)
    {
        var usernameResult = ValidateUsername(username);
        if (!usernameResult.IsSuccess)
        {
            return OperationResult<User>.Failure(usernameResult.ErrorMessage!);
        }

        var passwordResult = ValidatePassword(password);
        if (!passwordResult.IsSuccess)
        {
            return OperationResult<User>.Failure(passwordResult.ErrorMessage!);
        }

        var existingUser = await _userRepository.FindByUsernameAsync(usernameResult.Value!)
            .ConfigureAwait(false);
        if (existingUser is not null)
        {
            return OperationResult<User>.Failure(DuplicateUsernameMessage);
        }

        var user = new User(
            usernameResult.Value!,
            HashPassword(passwordResult.Value!),
            DateTimeOffset.UtcNow);

        if (BeforeRegistrationInsertAsync is { } beforeRegistrationInsertAsync)
        {
            await beforeRegistrationInsertAsync().ConfigureAwait(false);
        }

        long userId;
        try
        {
            userId = await _userRepository.AddAsync(user).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception) when (IsUsernameUniqueViolation(exception))
        {
            return OperationResult<User>.Failure(DuplicateUsernameMessage);
        }

        var persistedUser = await _userRepository.FindByIdAsync(userId).ConfigureAwait(false);

        if (persistedUser is null)
        {
            throw new InvalidOperationException("Unable to retrieve the registered user.");
        }

        return OperationResult<User>.Success(persistedUser);
    }

    public async Task<OperationResult<User>> LoginAsync(
        string username,
        string password,
        DateTimeOffset nowUtc)
    {
        nowUtc = nowUtc.ToUniversalTime();

        if (string.IsNullOrEmpty(username)
            || string.IsNullOrEmpty(password)
            || !ValidateUsername(username).IsSuccess)
        {
            return OperationResult<User>.Failure(InvalidCredentialsMessage);
        }

        var user = await _userRepository.FindByUsernameAsync(username).ConfigureAwait(false);
        if (user is null)
        {
            return OperationResult<User>.Failure(InvalidCredentialsMessage);
        }

        if (user.IsLocked(nowUtc))
        {
            return OperationResult<User>.Failure(ActiveLockoutMessage);
        }

        if (user.LockoutUntilUtc.HasValue)
        {
            user.ResetLoginFailures();
        }

        if (!VerifyPassword(password, user.PasswordHash))
        {
            user.RecordFailedLogin();
            if (user.FailedLoginAttempts >= MaximumFailedLoginAttempts)
            {
                user.ApplyLockout(nowUtc + LockoutDuration);
            }

            await _userRepository.UpdateLoginStateAsync(user).ConfigureAwait(false);
            return OperationResult<User>.Failure(InvalidCredentialsMessage);
        }

        user.ResetLoginFailures();
        await _userRepository.UpdateLoginStateAsync(user).ConfigureAwait(false);
        CurrentUser = user;
        return OperationResult<User>.Success(user);
    }

    public void Logout()
    {
        CurrentUser = null;
    }

    private static bool IsUsernameUniqueViolation(InvalidOperationException exception)
    {
        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        return sqliteException.SqliteErrorCode == SqliteConstraintErrorCode
            && sqliteException.SqliteExtendedErrorCode == SqliteConstraintUniqueErrorCode
            && sqliteException.Message.Contains(
                "UNIQUE constraint failed: Users.Username",
                StringComparison.Ordinal);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordHashIterations,
            HashAlgorithmName.SHA256,
            HashLength);

        return string.Join(
            '$',
            "v1",
            "PBKDF2-SHA256",
            PasswordHashIterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    private static bool VerifyPassword(string password, string storedPasswordHash)
    {
        if (string.IsNullOrEmpty(storedPasswordHash))
        {
            return false;
        }

        try
        {
            var segments = storedPasswordHash.Split('$', StringSplitOptions.None);
            if (segments.Length != 5
                || segments[0] != "v1"
                || segments[1] != "PBKDF2-SHA256"
                || !int.TryParse(
                    segments[2],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var iterations)
                || iterations != PasswordHashIterations)
            {
                return false;
            }

            var salt = Convert.FromBase64String(segments[3]);
            var expectedHash = Convert.FromBase64String(segments[4]);
            if (salt.Length != SaltLength || expectedHash.Length != HashLength)
            {
                return false;
            }

            var candidateHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                HashLength);
            return CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
