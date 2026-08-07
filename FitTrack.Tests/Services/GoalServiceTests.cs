using System;
using System.Globalization;
using System.Threading.Tasks;
using FitTrack.Common;
using FitTrack.Models;
using FitTrack.Repositories;
using FitTrack.Services;
using FitTrack.Tests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FitTrack.Tests.Services;

public sealed class GoalServiceTests
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 8, 3, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_RejectsNullRepository()
    {
        Assert.Throws<ArgumentNullException>(() => new GoalService(null!));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5000, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(-5000, false)]
    [InlineData(5001, false)]
    public void ValidateGoal_EnforcesTheApprovedRange(int targetCalories, bool expectedSuccess)
    {
        var service = CreateServiceWithoutDatabase();

        var result = service.ValidateGoal(targetCalories);

        Assert.Equal(expectedSuccess, result.IsSuccess);
        if (expectedSuccess)
        {
            Assert.Equal(targetCalories, result.Value);
            Assert.Null(result.ErrorMessage);
        }
        else
        {
            Assert.Equal(default, result.Value);
            Assert.Equal("Goal must be between 1 and 5,000 calories.", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task SaveGoalAsync_InsertsTheFirstGoalWithAUtcTimestamp()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "goal-first-user");
        var service = new GoalService(database.Goals);
        var offsetTime = new DateTimeOffset(
            2026,
            8,
            3,
            18,
            30,
            0,
            TimeSpan.FromHours(6.5)).AddTicks(1234);

        var result = await service.SaveGoalAsync(user, 2500, offsetTime);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.GoalId > 0);
        Assert.Equal(user.UserId, result.Value.UserId);
        Assert.Equal(2500, result.Value.TargetCalories);
        Assert.Equal(offsetTime.ToUniversalTime(), result.Value.UpdatedAtUtc);
        Assert.Equal(1, await CountGoalsAsync(database));

        var storedGoal = await ReadGoalAsync(database, user.UserId);
        Assert.NotNull(storedGoal);
        Assert.Equal(result.Value.GoalId, storedGoal!.GoalId);
        Assert.Equal(result.Value.UserId, storedGoal.UserId);
        Assert.Equal(result.Value.TargetCalories, storedGoal.TargetCalories);
        Assert.Equal(result.Value.UpdatedAtUtc, storedGoal.UpdatedAtUtc);
    }

    [Fact]
    public async Task SaveGoalAsync_UpdatesTheExistingGoalWithoutCreatingASecondRow()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "goal-update-user");
        var service = new GoalService(database.Goals);
        var firstResult = await service.SaveGoalAsync(user, 1800, BaseTime);
        var secondTime = BaseTime.AddDays(1).AddTicks(42);

        var secondResult = await service.SaveGoalAsync(user, 3200, secondTime);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.NotNull(firstResult.Value);
        Assert.NotNull(secondResult.Value);
        Assert.Equal(firstResult.Value!.GoalId, secondResult.Value!.GoalId);
        Assert.Equal(3200, secondResult.Value.TargetCalories);
        Assert.Equal(secondTime, secondResult.Value.UpdatedAtUtc);
        Assert.Equal(1, await CountGoalsAsync(database));

        var storedGoal = await ReadGoalAsync(database, user.UserId);
        Assert.NotNull(storedGoal);
        Assert.Equal(secondResult.Value.GoalId, storedGoal!.GoalId);
        Assert.Equal(3200, storedGoal.TargetCalories);
        Assert.Equal(secondTime, storedGoal.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetGoalAsync_ReturnsTheSavedGoalWithAllFields()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "goal-read-user");
        var savedGoal = await database.Goals.SaveAsync(new FitnessGoal(
            user.UserId,
            2100,
            BaseTime.AddHours(2)));
        var service = new GoalService(database.Goals);

        var result = await service.GetGoalAsync(user);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Value);
        Assert.Equal(savedGoal.GoalId, result.Value!.GoalId);
        Assert.Equal(savedGoal.UserId, result.Value.UserId);
        Assert.Equal(savedGoal.TargetCalories, result.Value.TargetCalories);
        Assert.Equal(savedGoal.UpdatedAtUtc, result.Value.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetGoalAsync_ReturnsSuccessfulNullWhenNoGoalExists()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "goal-none-user");
        var service = new GoalService(database.Goals);

        var result = await service.GetGoalAsync(user);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task GetGoalAsync_ReturnsSuccessfulNullForAPositiveMissingUserId()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var missingUser = CreateMissingUser(99999);
        var service = new GoalService(database.Goals);

        var result = await service.GetGoalAsync(missingUser);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task GetGoalAsync_RejectsNullAndUnsavedUsers()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new GoalService(database.Goals);
        var unsavedUser = new User("unsaved-read-goal-user", "fake-hash", BaseTime);

        var nullResult = await service.GetGoalAsync(null);
        var unsavedResult = await service.GetGoalAsync(unsavedUser);

        Assert.False(nullResult.IsSuccess);
        Assert.Equal("A registered user is required to manage a goal.", nullResult.ErrorMessage);
        Assert.False(unsavedResult.IsSuccess);
        Assert.Equal(
            "A registered user is required to manage a goal.",
            unsavedResult.ErrorMessage);
    }

    [Fact]
    public async Task SaveGoalAsync_RejectsNullUserWithoutWriting()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new GoalService(database.Goals);

        var result = await service.SaveGoalAsync(null, 2000, BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal("A registered user is required to manage a goal.", result.ErrorMessage);
        Assert.Null(result.Value);
        Assert.Equal(0, await CountGoalsAsync(database));
    }

    [Fact]
    public async Task SaveGoalAsync_RejectsAnUnsavedUserWithoutWriting()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new GoalService(database.Goals);
        var user = new User("unsaved-goal-user", "fake-hash", BaseTime);

        var result = await service.SaveGoalAsync(user, 2000, BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal("A registered user is required to manage a goal.", result.ErrorMessage);
        Assert.Equal(0, await CountGoalsAsync(database));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5001)]
    public async Task SaveGoalAsync_RejectsInvalidTargetsWithoutWriting(int targetCalories)
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "invalid-save-goal-user");
        var service = new GoalService(database.Goals);

        var result = await service.SaveGoalAsync(user, targetCalories, BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal("Goal must be between 1 and 5,000 calories.", result.ErrorMessage);
        Assert.Equal(0, await CountGoalsAsync(database));
    }

    [Fact]
    public async Task GoalRemainsActiveWhenRetrievedAfterALaterDate()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "goal-later-date-user");
        var service = new GoalService(database.Goals);
        var savedResult = await service.SaveGoalAsync(user, 2200, BaseTime);

        var retrievedResult = await service.GetGoalAsync(user);

        Assert.True(savedResult.IsSuccess);
        Assert.True(retrievedResult.IsSuccess);
        Assert.Equal(savedResult.Value!.GoalId, retrievedResult.Value!.GoalId);
        Assert.Equal(savedResult.Value.TargetCalories, retrievedResult.Value.TargetCalories);
        Assert.Equal(1, await CountGoalsAsync(database));
    }

    [Fact]
    public async Task SaveGoalAsync_PreservesForeignKeyRepositoryFailures()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = new GoalService(database.Goals);
        var missingUser = CreateMissingUser(99998);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveGoalAsync(missingUser, 2000, BaseTime));

        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Equal(19, sqliteException.SqliteErrorCode & 0xFF);
        Assert.Equal(0, await CountGoalsAsync(database));
    }

    [Fact]
    public async Task GetGoalAsync_PreservesRepositoryFailures()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "goal-missing-table-user");
        await DropTableAsync(database, "FitnessGoals");
        var service = new GoalService(database.Goals);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetGoalAsync(user));

        Assert.IsType<SqliteException>(exception.InnerException);
    }

    private static GoalService CreateServiceWithoutDatabase()
    {
        return new GoalService(new GoalRepository("Data Source=:memory:"));
    }

    private static async Task<User> CreateUserAsync(
        RepositoryTestDatabase database,
        string username)
    {
        var userId = await database.Users.AddAsync(new User(username, "fake-hash", BaseTime));
        var user = await database.Users.FindByIdAsync(userId);
        Assert.NotNull(user);
        return user!;
    }

    private static User CreateMissingUser(long userId)
    {
        return new User(userId, "missing-goal-user", "fake-hash", 0, null, BaseTime);
    }

    private static async Task<long> CountGoalsAsync(RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM FitnessGoals;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<FitnessGoal?> ReadGoalAsync(
        RepositoryTestDatabase database,
        long userId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT GoalId, UserId, TargetCalories, UpdatedAtUtc
            FROM FitnessGoals
            WHERE UserId = $userId;
            """;
        command.Parameters.Add(RepositoryTestDatabase.Parameter("$userId", userId));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new FitnessGoal(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetDouble(2),
            DateTimeOffset.Parse(
                reader.GetString(3),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));
    }

    private static async Task DropTableAsync(
        RepositoryTestDatabase database,
        string tableName)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {tableName};";
        await command.ExecuteNonQueryAsync();
    }
}
