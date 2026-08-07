using System;
using System.Threading.Tasks;
using FitTrack.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FitTrack.Tests.Repositories;

public sealed class GoalRepositoryTests
{
    [Fact]
    public async Task SaveAsync_InsertsThenUpdatesExistingGoal_AndPreservesGoalId()
    {
        await using var database = await Data.RepositoryTestDatabase.CreateAsync();
        var userId = await database.Users.AddAsync(new User(
            "goal-user",
            "fake-hash",
            new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero)));
        var firstTimestamp = new DateTimeOffset(2026, 8, 3, 1, 0, 0, TimeSpan.FromHours(6.5));
        var secondTimestamp = firstTimestamp.AddHours(2);

        var firstSavedGoal = await database.Goals.SaveAsync(new FitnessGoal(userId, 2000, firstTimestamp));
        var secondSavedGoal = await database.Goals.SaveAsync(new FitnessGoal(userId, 2500, secondTimestamp));
        var retrievedGoal = await database.Goals.GetByUserIdAsync(userId);

        Assert.True(firstSavedGoal.GoalId > 0);
        Assert.Equal(firstSavedGoal.GoalId, secondSavedGoal.GoalId);
        Assert.Equal(2500, secondSavedGoal.TargetCalories);
        Assert.Equal(secondTimestamp.ToUniversalTime(), secondSavedGoal.UpdatedAtUtc);
        Assert.NotNull(retrievedGoal);
        Assert.Equal(secondSavedGoal.GoalId, retrievedGoal!.GoalId);
        Assert.Equal(2500, retrievedGoal.TargetCalories);

        await using var connection = await database.OpenConnectionAsync();
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM FitnessGoals WHERE UserId = $userId;";
        countCommand.Parameters.Add(Data.RepositoryTestDatabase.Parameter("$userId", userId));
        Assert.Equal(1, Convert.ToInt32(await countCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task SaveAsync_RejectsGoalForMissingUser_WithForeignKeyConstraintInnerException()
    {
        await using var database = await Data.RepositoryTestDatabase.CreateAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => database.Goals.SaveAsync(
            new FitnessGoal(
                99999,
                2000,
                new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero))));
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);

        Assert.Equal(19, sqliteException.SqliteErrorCode & 0xFF);
    }
}
