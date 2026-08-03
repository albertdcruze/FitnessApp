using System;
using System.Threading.Tasks;
using FitnessApp.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FitnessApp.Tests.Repositories;

public sealed class ActivityRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsEveryActivityField()
    {
        await using var database = await Data.RepositoryTestDatabase.CreateAsync();
        var userId = await database.Users.AddAsync(new User(
            "activity-user",
            "fake-hash",
            new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero)));
        var recordedAtUtc = new DateTimeOffset(2026, 8, 3, 1, 2, 3, TimeSpan.FromHours(6.5));
        var activity = new ActivityRecord(userId, ActivityType.Walking, 2.5, 30, 100, 250.75, recordedAtUtc);

        var activityRecordId = await database.Activities.AddAsync(activity);

        Assert.True(activityRecordId > 0);
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UserId, ActivityType, Metric1Value, Metric2Value, Metric3Value,
                   CaloriesBurned, RecordedAtUtc
            FROM ActivityRecords
            WHERE ActivityRecordId = $activityRecordId;
            """;
        command.Parameters.Add(Data.RepositoryTestDatabase.Parameter("$activityRecordId", activityRecordId));

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(userId, reader.GetInt64(0));
        Assert.Equal("Walking", reader.GetString(1));
        Assert.Equal(2.5, reader.GetDouble(2));
        Assert.Equal(30, reader.GetDouble(3));
        Assert.Equal(100, reader.GetDouble(4));
        Assert.Equal(250.75, reader.GetDouble(5));
        Assert.Equal(recordedAtUtc.ToUniversalTime(), DateTimeOffset.Parse(reader.GetString(6)).ToUniversalTime());
    }

    [Fact]
    public async Task GetCaloriesTotalAsync_UsesInclusiveStartAndExclusiveEndForOneUser()
    {
        await using var database = await Data.RepositoryTestDatabase.CreateAsync();
        var targetUserId = await database.Users.AddAsync(new User(
            "target-user",
            "fake-target-hash",
            new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero)));
        var otherUserId = await database.Users.AddAsync(new User(
            "other-user",
            "fake-other-hash",
            new DateTimeOffset(2026, 8, 3, 0, 0, 1, TimeSpan.Zero)));
        var startUtc = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);
        var endUtc = startUtc.AddDays(1);

        await database.Activities.AddAsync(CreateActivity(targetUserId, startUtc.AddMinutes(-1), 10));
        await database.Activities.AddAsync(CreateActivity(targetUserId, startUtc, 100.5));
        await database.Activities.AddAsync(CreateActivity(targetUserId, startUtc.AddHours(12), 200.25));
        await database.Activities.AddAsync(CreateActivity(targetUserId, endUtc, 300));
        await database.Activities.AddAsync(CreateActivity(targetUserId, endUtc.AddMinutes(1), 400));
        await database.Activities.AddAsync(CreateActivity(otherUserId, startUtc.AddHours(12), 500));

        var total = await database.Activities.GetCaloriesTotalAsync(targetUserId, startUtc, endUtc);
        var emptyTotal = await database.Activities.GetCaloriesTotalAsync(
            targetUserId,
            endUtc.AddDays(1),
            endUtc.AddDays(2));

        Assert.Equal(300.75, total, precision: 10);
        Assert.Equal(0.0, emptyTotal);
    }

    [Fact]
    public async Task AddAsync_RejectsActivityForMissingUser_WithForeignKeyConstraintInnerException()
    {
        await using var database = await Data.RepositoryTestDatabase.CreateAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => database.Activities.AddAsync(
            CreateActivity(99999, new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero), 50)));
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);

        Assert.Equal(19, sqliteException.SqliteErrorCode & 0xFF);
    }

    [Fact]
    public async Task AddAsync_RejectsInvalidActivityType_AndDoesNotPersistIt()
    {
        await using var database = await Data.RepositoryTestDatabase.CreateAsync();
        var userId = await database.Users.AddAsync(new User(
            "invalid-activity-user",
            "fake-hash",
            new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => database.Activities.AddAsync(
            new ActivityRecord(
                userId,
                (ActivityType)999,
                1,
                1,
                1,
                1,
                new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero))));
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);

        Assert.Equal(19, sqliteException.SqliteErrorCode & 0xFF);

        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ActivityRecords WHERE UserId = $userId;";
        command.Parameters.Add(Data.RepositoryTestDatabase.Parameter("$userId", userId));
        Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task CascadeDelete_RemovesUserGoalAndActivities()
    {
        await using var database = await Data.RepositoryTestDatabase.CreateAsync();
        var userId = await database.Users.AddAsync(new User(
            "cascade-user",
            "fake-hash",
            new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero)));
        await database.Goals.SaveAsync(new FitnessGoal(
            userId,
            2000,
            new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero)));
        await database.Activities.AddAsync(CreateActivity(
            userId,
            new DateTimeOffset(2026, 8, 3, 1, 0, 0, TimeSpan.Zero),
            100));

        await using var connection = await database.OpenConnectionAsync();
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = "DELETE FROM Users WHERE UserId = $userId;";
            deleteCommand.Parameters.Add(Data.RepositoryTestDatabase.Parameter("$userId", userId));
            Assert.Equal(1, await deleteCommand.ExecuteNonQueryAsync());
        }

        foreach (var tableName in new[] { "Users", "FitnessGoals", "ActivityRecords" })
        {
            await using var countCommand = connection.CreateCommand();
            countCommand.CommandText = tableName switch
            {
                "Users" => "SELECT COUNT(*) FROM Users WHERE UserId = $userId;",
                "FitnessGoals" => "SELECT COUNT(*) FROM FitnessGoals WHERE UserId = $userId;",
                _ => "SELECT COUNT(*) FROM ActivityRecords WHERE UserId = $userId;"
            };
            countCommand.Parameters.Add(Data.RepositoryTestDatabase.Parameter("$userId", userId));
            Assert.Equal(0, Convert.ToInt32(await countCommand.ExecuteScalarAsync()));
        }
    }

    private static ActivityRecord CreateActivity(long userId, DateTimeOffset recordedAtUtc, double calories)
    {
        return new ActivityRecord(userId, ActivityType.Walking, 1, 2, 3, calories, recordedAtUtc);
    }
}
