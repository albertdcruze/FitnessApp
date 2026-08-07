using System;
using System.Globalization;
using System.Threading.Tasks;
using FitTrack.Models;
using Microsoft.Data.Sqlite;

namespace FitTrack.Repositories;

public sealed class GoalRepository
{
    private readonly string _connectionString;

    public GoalRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQLite connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<FitnessGoal?> GetByUserIdAsync(long userId)
    {
        try
        {
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT GoalId, UserId, TargetCalories, UpdatedAtUtc
                FROM FitnessGoals
                WHERE UserId = $userId
                LIMIT 1;
                """;
            command.Parameters.Add(CreateParameter("$userId", userId));

            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                return null;
            }

            return MapGoal(reader);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException("Unable to find the fitness goal.", exception);
        }
    }

    public async Task<FitnessGoal> SaveAsync(FitnessGoal goal)
    {
        try
        {
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            await using var transaction = (SqliteTransaction)await connection
                .BeginTransactionAsync()
                .ConfigureAwait(false);

            await using (var upsertCommand = connection.CreateCommand())
            {
                upsertCommand.Transaction = transaction;
                upsertCommand.CommandText = """
                    INSERT INTO FitnessGoals (UserId, TargetCalories, UpdatedAtUtc)
                    VALUES ($userId, $targetCalories, $updatedAtUtc)
                    ON CONFLICT(UserId) DO UPDATE SET
                        TargetCalories = excluded.TargetCalories,
                        UpdatedAtUtc = excluded.UpdatedAtUtc;
                    """;
                upsertCommand.Parameters.Add(CreateParameter("$userId", goal.UserId));
                upsertCommand.Parameters.Add(CreateParameter("$targetCalories", goal.TargetCalories));
                upsertCommand.Parameters.Add(CreateParameter("$updatedAtUtc", FormatUtc(goal.UpdatedAtUtc)));
                await upsertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            FitnessGoal storedGoal;
            await using (var selectCommand = connection.CreateCommand())
            {
                selectCommand.Transaction = transaction;
                selectCommand.CommandText = """
                    SELECT GoalId, UserId, TargetCalories, UpdatedAtUtc
                    FROM FitnessGoals
                    WHERE UserId = $userId
                    LIMIT 1;
                    """;
                selectCommand.Parameters.Add(CreateParameter("$userId", goal.UserId));

                await using var reader = await selectCommand.ExecuteReaderAsync().ConfigureAwait(false);
                if (!await reader.ReadAsync().ConfigureAwait(false))
                {
                    throw new InvalidOperationException("Unable to read the saved fitness goal.");
                }

                storedGoal = MapGoal(reader);
            }

            await transaction.CommitAsync().ConfigureAwait(false);
            return storedGoal;
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException("Unable to save the fitness goal.", exception);
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

    private static FitnessGoal MapGoal(SqliteDataReader reader)
    {
        return new FitnessGoal(
            reader.GetInt64(reader.GetOrdinal("GoalId")),
            reader.GetInt64(reader.GetOrdinal("UserId")),
            reader.GetDouble(reader.GetOrdinal("TargetCalories")),
            ParseUtc(reader.GetString(reader.GetOrdinal("UpdatedAtUtc")), "UpdatedAtUtc"));
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
