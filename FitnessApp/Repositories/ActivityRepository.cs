using System;
using System.Globalization;
using System.Threading.Tasks;
using FitnessApp.Models;
using Microsoft.Data.Sqlite;

namespace FitnessApp.Repositories;

public sealed class ActivityRepository
{
    private readonly string _connectionString;

    public ActivityRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQLite connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task<long> AddAsync(ActivityRecord activityRecord)
    {
        try
        {
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = """
                INSERT INTO ActivityRecords (
                    UserId,
                    ActivityType,
                    Metric1Value,
                    Metric2Value,
                    Metric3Value,
                    CaloriesBurned,
                    RecordedAtUtc
                )
                VALUES (
                    $userId,
                    $activityType,
                    $metric1Value,
                    $metric2Value,
                    $metric3Value,
                    $caloriesBurned,
                    $recordedAtUtc
                );
                """;
            insertCommand.Parameters.Add(CreateParameter("$userId", activityRecord.UserId));
            insertCommand.Parameters.Add(CreateParameter("$activityType", activityRecord.ActivityType.ToString()));
            insertCommand.Parameters.Add(CreateParameter("$metric1Value", activityRecord.Metric1Value));
            insertCommand.Parameters.Add(CreateParameter("$metric2Value", activityRecord.Metric2Value));
            insertCommand.Parameters.Add(CreateParameter("$metric3Value", activityRecord.Metric3Value));
            insertCommand.Parameters.Add(CreateParameter("$caloriesBurned", activityRecord.CaloriesBurned));
            insertCommand.Parameters.Add(CreateParameter("$recordedAtUtc", FormatUtc(activityRecord.RecordedAtUtc)));
            await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);

            await using var idCommand = connection.CreateCommand();
            idCommand.CommandText = "SELECT last_insert_rowid();";
            var idValue = await idCommand.ExecuteScalarAsync().ConfigureAwait(false);
            return Convert.ToInt64(idValue, CultureInfo.InvariantCulture);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException("Unable to add the activity record.", exception);
        }
    }

    public async Task<double> GetCaloriesTotalAsync(
        long userId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc)
    {
        try
        {
            await using var connection = await OpenConnectionAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COALESCE(SUM(CaloriesBurned), 0.0)
                FROM ActivityRecords
                WHERE UserId = $userId
                  AND RecordedAtUtc >= $startUtc
                  AND RecordedAtUtc < $endUtc;
                """;
            command.Parameters.Add(CreateParameter("$userId", userId));
            command.Parameters.Add(CreateParameter("$startUtc", FormatUtc(startUtc)));
            command.Parameters.Add(CreateParameter("$endUtc", FormatUtc(endUtc)));

            var total = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return Convert.ToDouble(total, CultureInfo.InvariantCulture);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException("Unable to calculate the stored calorie total.", exception);
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

    private static SqliteParameter CreateParameter(string name, object? value)
    {
        return new SqliteParameter(name, value ?? DBNull.Value);
    }

    private static string FormatUtc(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }
}
