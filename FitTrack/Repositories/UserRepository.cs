using System;
using System.Globalization;
using System.Threading.Tasks;
using FitTrack.Models;
using Microsoft.Data.Sqlite;

namespace FitTrack.Repositories;

public sealed class UserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQLite connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<User?> FindByUsernameAsync(string username)
    {
        try
        {
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT UserId, Username, PasswordHash, FailedLoginAttempts,
                       LockoutUntilUtc, CreatedAtUtc
                FROM Users
                WHERE Username = $username COLLATE NOCASE
                LIMIT 1;
                """;
            command.Parameters.Add(CreateParameter("$username", username));

            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return MapUser(reader);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException("Unable to find the user by username.", exception);
        }
    }

    public async Task<User?> FindByIdAsync(long userId)
    {
        try
        {
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT UserId, Username, PasswordHash, FailedLoginAttempts,
                       LockoutUntilUtc, CreatedAtUtc
                FROM Users
                WHERE UserId = $userId
                LIMIT 1;
                """;
            command.Parameters.Add(CreateParameter("$userId", userId));

            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return MapUser(reader);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException("Unable to find the user by ID.", exception);
        }
    }

    public async Task<long> AddAsync(User user)
    {
        try
        {
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = """
                INSERT INTO Users (
                    Username,
                    PasswordHash,
                    FailedLoginAttempts,
                    LockoutUntilUtc,
                    CreatedAtUtc
                )
                VALUES (
                    $username,
                    $passwordHash,
                    $failedLoginAttempts,
                    $lockoutUntilUtc,
                    $createdAtUtc
                );
                """;
            insertCommand.Parameters.Add(CreateParameter("$username", user.Username));
            insertCommand.Parameters.Add(CreateParameter("$passwordHash", user.PasswordHash));
            insertCommand.Parameters.Add(CreateParameter("$failedLoginAttempts", user.FailedLoginAttempts));
            insertCommand.Parameters.Add(CreateParameter(
                "$lockoutUntilUtc",
                user.LockoutUntilUtc.HasValue
                    ? FormatUtc(user.LockoutUntilUtc.Value)
                    : DBNull.Value));
            insertCommand.Parameters.Add(CreateParameter("$createdAtUtc", FormatUtc(user.CreatedAtUtc)));
            await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);

            await using var idCommand = connection.CreateCommand();
            idCommand.CommandText = "SELECT last_insert_rowid();";
            var idValue = await idCommand.ExecuteScalarAsync().ConfigureAwait(false);
            return Convert.ToInt64(idValue, CultureInfo.InvariantCulture);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException("Unable to add the user to the database.", exception);
        }
    }

    public async Task UpdateLoginStateAsync(User user)
    {
        try
        {
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Users
                SET FailedLoginAttempts = $failedLoginAttempts,
                    LockoutUntilUtc = $lockoutUntilUtc
                WHERE UserId = $userId;
                """;
            command.Parameters.Add(CreateParameter("$failedLoginAttempts", user.FailedLoginAttempts));
            command.Parameters.Add(CreateParameter(
                "$lockoutUntilUtc",
                user.LockoutUntilUtc.HasValue
                    ? FormatUtc(user.LockoutUntilUtc.Value)
                    : DBNull.Value));
            command.Parameters.Add(CreateParameter("$userId", user.UserId));

            var affectedRows = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            if (affectedRows == 0)
            {
                throw new InvalidOperationException(
                    "Unable to update the user login state because the stored user could not be found.");
            }
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException("Unable to update the user login state.", exception);
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON;";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static User MapUser(SqliteDataReader reader)
    {
        var lockoutOrdinal = reader.GetOrdinal("LockoutUntilUtc");
        var lockoutUntilUtc = reader.IsDBNull(lockoutOrdinal)
            ? (DateTimeOffset?)null
            : ParseUtc(reader.GetString(lockoutOrdinal), "LockoutUntilUtc");

        return new User(
            reader.GetInt64(reader.GetOrdinal("UserId")),
            reader.GetString(reader.GetOrdinal("Username")),
            reader.GetString(reader.GetOrdinal("PasswordHash")),
            reader.GetInt32(reader.GetOrdinal("FailedLoginAttempts")),
            lockoutUntilUtc,
            ParseUtc(reader.GetString(reader.GetOrdinal("CreatedAtUtc")), "CreatedAtUtc"));
    }

    private static SqliteParameter CreateParameter(string name, object? value)
    {
        return new SqliteParameter(name, value ?? DBNull.Value);
    }

    private static string FormatUtc(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseUtc(string value, string columnName)
    {
        try
        {
            return DateTimeOffset.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"The stored {columnName} timestamp is invalid.",
                exception);
        }
    }
}
