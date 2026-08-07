using System;
using System.IO;
using System.Threading.Tasks;
using FitTrack.Data;
using FitTrack.Repositories;
using Microsoft.Data.Sqlite;

namespace FitTrack.Tests.Data;

public sealed class RepositoryTestDatabase : IAsyncDisposable
{
    private readonly string _directoryPath;

    private RepositoryTestDatabase(string directoryPath, string connectionString)
    {
        _directoryPath = directoryPath;
        ConnectionString = connectionString;
        Users = new UserRepository(connectionString);
        Goals = new GoalRepository(connectionString);
        Activities = new ActivityRepository(connectionString);
    }

    public string ConnectionString { get; }

    public UserRepository Users { get; }

    public GoalRepository Goals { get; }

    public ActivityRepository Activities { get; }

    public static async Task<RepositoryTestDatabase> CreateAsync()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "FitTrack.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        var databasePath = Path.Combine(directoryPath, "fittrack.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
        var database = new RepositoryTestDatabase(directoryPath, connectionString);

        try
        {
            await new DatabaseInitializer(connectionString).InitializeAsync().ConfigureAwait(false);
            return database;
        }
        catch
        {
            await database.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
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

    public static SqliteParameter Parameter(string name, object? value)
    {
        return new SqliteParameter(name, value ?? DBNull.Value);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
