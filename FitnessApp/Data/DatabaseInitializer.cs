using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace FitnessApp.Data;

public sealed class DatabaseInitializer
{
    private const string UsersTableSql = """
        CREATE TABLE IF NOT EXISTS Users (
            UserId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Username TEXT NOT NULL COLLATE NOCASE UNIQUE,
            PasswordHash TEXT NOT NULL,
            FailedLoginAttempts INTEGER NOT NULL DEFAULT 0 CHECK (FailedLoginAttempts >= 0),
            LockoutUntilUtc TEXT NULL DEFAULT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """;

    private const string FitnessGoalsTableSql = """
        CREATE TABLE IF NOT EXISTS FitnessGoals (
            GoalId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL UNIQUE,
            TargetCalories REAL NOT NULL CHECK (TargetCalories BETWEEN 1 AND 5000),
            UpdatedAtUtc TEXT NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
        );
        """;

    private const string ActivityRecordsTableSql = """
        CREATE TABLE IF NOT EXISTS ActivityRecords (
            ActivityRecordId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            ActivityType TEXT NOT NULL CHECK (ActivityType IN (
                'Walking',
                'Swimming',
                'Running',
                'Cycling',
                'StationaryRowing',
                'StrengthTraining'
            )),
            Metric1Value REAL NOT NULL CHECK (Metric1Value > 0),
            Metric2Value REAL NOT NULL CHECK (Metric2Value > 0),
            Metric3Value REAL NOT NULL CHECK (Metric3Value > 0),
            CaloriesBurned REAL NOT NULL CHECK (CaloriesBurned >= 0),
            RecordedAtUtc TEXT NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
        );
        """;

    private const string ActivityRecordsIndexSql = """
        CREATE INDEX IF NOT EXISTS IX_ActivityRecords_UserId_RecordedAtUtc
        ON ActivityRecords (UserId, RecordedAtUtc);
        """;

    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQLite connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using (var foreignKeysCommand = connection.CreateCommand())
        {
            foreignKeysCommand.CommandText = "PRAGMA foreign_keys = ON;";
            await foreignKeysCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync().ConfigureAwait(false);
        await ExecuteCommandAsync(connection, transaction, UsersTableSql).ConfigureAwait(false);
        await ExecuteCommandAsync(connection, transaction, FitnessGoalsTableSql).ConfigureAwait(false);
        await ExecuteCommandAsync(connection, transaction, ActivityRecordsTableSql).ConfigureAwait(false);
        await ExecuteCommandAsync(connection, transaction, ActivityRecordsIndexSql).ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
    }

    private static async Task ExecuteCommandAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
