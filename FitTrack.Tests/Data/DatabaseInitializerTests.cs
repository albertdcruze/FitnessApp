using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitTrack.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FitTrack.Tests.Data;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_CanRunTwice_AndPreservesExistingData()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var createdAtUtc = new DateTimeOffset(2026, 8, 3, 1, 2, 3, TimeSpan.Zero);
        var userId = await database.Users.AddAsync(new User("repeat-user", "fake-hash", createdAtUtc));

        await new FitTrack.Data.DatabaseInitializer(database.ConnectionString)
            .InitializeAsync();

        await using var connection = await database.OpenConnectionAsync();
        await using var objectCommand = connection.CreateCommand();
        objectCommand.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type IN ('table', 'index');
            """;

        var objectNames = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await objectCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                objectNames.Add(reader.GetString(0));
            }
        }

        Assert.Contains("Users", objectNames);
        Assert.Contains("FitnessGoals", objectNames);
        Assert.Contains("ActivityRecords", objectNames);
        Assert.Contains("IX_ActivityRecords_UserId_RecordedAtUtc", objectNames);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Users WHERE UserId = $userId;";
        countCommand.Parameters.Add(RepositoryTestDatabase.Parameter("$userId", userId));
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        Assert.Equal(1, count);
    }
}
