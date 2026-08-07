using System;
using System.Globalization;
using System.Threading.Tasks;
using FitTrack.Models;
using FitTrack.Repositories;
using FitTrack.Services;
using FitTrack.Tests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FitTrack.Tests.Services;

public sealed class ProgressServiceTests
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 8, 3, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_RejectsNullGoalRepository()
    {
        Assert.Throws<ArgumentNullException>(() => new ProgressService(
            null!,
            new ActivityRepository("Data Source=:memory:")));
    }

    [Fact]
    public void Constructor_RejectsNullActivityRepository()
    {
        Assert.Throws<ArgumentNullException>(() => new ProgressService(
            new GoalRepository("Data Source=:memory:"),
            null!));
    }

    [Fact]
    public async Task GetTodayProgressAsync_RejectsANullUserWithoutRepositoryResults()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(
            null,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);

        Assert.False(result.IsSuccess);
        Assert.Equal("A registered user is required to view progress.", result.ErrorMessage);
        Assert.Null(result.Value);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetTodayProgressAsync_RejectsNonPositiveUserIds(long userId)
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = CreateService(database);
        var user = userId == 0
            ? new User("unsaved-progress-user", "fake-hash", BaseTime)
            : new User(userId, "invalid-progress-user", "fake-hash", 0, null, BaseTime);

        var result = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);

        Assert.False(result.IsSuccess);
        Assert.Equal("A registered user is required to view progress.", result.ErrorMessage);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetTodayProgressAsync_RejectsANullTimeZoneWithoutRepositoryResults()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "null-time-zone-user");
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            null);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "A valid time zone is required to calculate daily progress.",
            result.ErrorMessage);
        Assert.Null(result.Value);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Fact]
    public async Task GetTodayProgressAsync_ReturnsStoredCaloriesWhenNoGoalExists()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "no-goal-progress-user");
        await AddActivityAsync(
            database,
            user.UserId,
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero),
            123.456);
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.False(result.Value!.HasGoal);
        Assert.Equal(0, result.Value.TargetCalories);
        Assert.Equal(123.456, result.Value.TotalCalories);
        Assert.Equal(0, result.Value.RemainingCalories);
        Assert.Equal(0, result.Value.ProgressPercentage);
        Assert.False(result.Value.IsGoalAchieved);
        Assert.Equal("No daily calorie goal has been set.", result.Value.StatusMessage);
    }

    [Fact]
    public async Task GetTodayProgressAsync_ReturnsZeroProgressWhenGoalHasNoActivity()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "zero-progress-user");
        await database.Goals.SaveAsync(new FitnessGoal(user.UserId, 300, BaseTime));
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);

        Assert.True(result.IsSuccess);
        Assert.Equal(300, result.Value!.TargetCalories);
        Assert.Equal(0, result.Value.TotalCalories);
        Assert.Equal(300, result.Value.RemainingCalories);
        Assert.Equal(0, result.Value.ProgressPercentage);
        Assert.False(result.Value.IsGoalAchieved);
        Assert.Equal("Goal not achieved yet.", result.Value.StatusMessage);
    }

    [Fact]
    public async Task GetTodayProgressAsync_CalculatesProgressBelowTheGoal()
    {
        var summary = await GetProgressWithGoalAndCaloriesAsync(300, 120);

        Assert.Equal(300, summary.TargetCalories);
        Assert.Equal(120, summary.TotalCalories);
        Assert.Equal(180, summary.RemainingCalories);
        Assert.Equal(40, summary.ProgressPercentage);
        Assert.False(summary.IsGoalAchieved);
        Assert.Equal("Goal not achieved yet.", summary.StatusMessage);
    }

    [Fact]
    public async Task GetTodayProgressAsync_MarksAnExactTargetAsAchieved()
    {
        var summary = await GetProgressWithGoalAndCaloriesAsync(300, 300);

        Assert.Equal(0, summary.RemainingCalories);
        Assert.Equal(100, summary.ProgressPercentage);
        Assert.True(summary.IsGoalAchieved);
        Assert.Equal("Goal achieved.", summary.StatusMessage);
    }

    [Fact]
    public async Task GetTodayProgressAsync_PreservesAnUncappedPercentageAboveTheGoal()
    {
        var summary = await GetProgressWithGoalAndCaloriesAsync(300, 450);

        Assert.Equal(0, summary.RemainingCalories);
        Assert.Equal(150, summary.ProgressPercentage);
        Assert.True(summary.ProgressPercentage > 100);
        Assert.True(summary.IsGoalAchieved);
    }

    [Fact]
    public async Task GetTodayProgressAsync_OnlyCountsTheRequestedUser()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var targetUser = await CreateUserAsync(database, "target-progress-user");
        var otherUser = await CreateUserAsync(database, "other-progress-user");
        var activityTime = new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
        await AddActivityAsync(database, targetUser.UserId, activityTime, 120);
        await AddActivityAsync(database, otherUser.UserId, activityTime, 900);
        await database.Goals.SaveAsync(new FitnessGoal(targetUser.UserId, 300, BaseTime));
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(
            targetUser,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);

        Assert.True(result.IsSuccess);
        Assert.Equal(120, result.Value!.TotalCalories);
        Assert.Equal(180, result.Value.RemainingCalories);
    }

    [Fact]
    public async Task GetTodayProgressAsync_UsesAHalfOpenUtcDayRange()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "half-open-progress-user");
        var startUtc = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var endUtc = startUtc.AddDays(1);
        await AddActivityAsync(database, user.UserId, startUtc.AddTicks(-1), 1);
        await AddActivityAsync(database, user.UserId, startUtc, 2);
        await AddActivityAsync(database, user.UserId, startUtc.AddHours(12), 3);
        await AddActivityAsync(database, user.UserId, endUtc, 4);
        await AddActivityAsync(database, user.UserId, endUtc.AddTicks(1), 5);
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.TotalCalories);
    }

    [Fact]
    public async Task GetTodayProgressAsync_ConvertsUtcPlusSixThirtyLocalDayBoundaries()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "offset-progress-user");
        var timeZone = CreateUtcPlusSixThirtyTimeZone();
        var startUtc = new DateTimeOffset(2026, 8, 3, 17, 30, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 8, 4, 17, 30, 0, TimeSpan.Zero);
        await AddActivityAsync(database, user.UserId, startUtc.AddTicks(-1), 1);
        await AddActivityAsync(database, user.UserId, startUtc, 2);
        await AddActivityAsync(database, user.UserId, startUtc.AddHours(12), 3);
        await AddActivityAsync(database, user.UserId, endUtc, 4);
        await AddActivityAsync(database, user.UserId, endUtc.AddTicks(1), 5);
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            timeZone);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.TotalCalories);
    }

    [Fact]
    public async Task GetTodayProgressAsync_UsesA23HourSpringDstDay()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "spring-dst-progress-user");
        var timeZone = GetNewYorkTimeZone();
        var localDate = new DateOnly(2026, 3, 8);
        var (startUtc, endUtc) = ConvertBoundaries(localDate, timeZone);
        Assert.Equal(TimeSpan.FromHours(23), endUtc - startUtc);
        await AddActivityAsync(database, user.UserId, startUtc, 10);
        await AddActivityAsync(database, user.UserId, endUtc.AddTicks(-1), 20);
        await AddActivityAsync(database, user.UserId, endUtc, 40);
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(user, localDate, timeZone);

        Assert.True(result.IsSuccess);
        Assert.Equal(30, result.Value!.TotalCalories);
    }

    [Fact]
    public async Task GetTodayProgressAsync_UsesA25HourAutumnDstDay()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "autumn-dst-progress-user");
        var timeZone = GetNewYorkTimeZone();
        var localDate = new DateOnly(2026, 11, 1);
        var (startUtc, endUtc) = ConvertBoundaries(localDate, timeZone);
        Assert.Equal(TimeSpan.FromHours(25), endUtc - startUtc);
        await AddActivityAsync(database, user.UserId, startUtc, 10);
        await AddActivityAsync(database, user.UserId, endUtc.AddTicks(-1), 20);
        await AddActivityAsync(database, user.UserId, endUtc, 40);
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(user, localDate, timeZone);

        Assert.True(result.IsSuccess);
        Assert.Equal(30, result.Value!.TotalCalories);
    }

    [Fact]
    public async Task GetTodayProgressAsync_PreservesStoredCaloriePrecision()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "stored-calorie-progress-user");
        await AddActivityAsync(
            database,
            user.UserId,
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero),
            123.456);
        await database.Goals.SaveAsync(new FitnessGoal(user.UserId, 500, BaseTime));
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);

        Assert.True(result.IsSuccess);
        Assert.Equal(123.456, result.Value!.TotalCalories);
        Assert.Equal(376.544, result.Value.RemainingCalories);
        Assert.Equal(123.456 / 500 * 100, result.Value.ProgressPercentage);
    }

    [Fact]
    public async Task GetTodayProgressAsync_DoesNotMutateGoalsOrActivitiesAndLaterDatesOnlyFilterRows()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "no-mutation-progress-user");
        var activityId = await AddActivityAsync(
            database,
            user.UserId,
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero),
            120);
        var goal = await database.Goals.SaveAsync(new FitnessGoal(user.UserId, 300, BaseTime));
        var beforeActivity = await ReadActivityAsync(database, activityId);
        var service = CreateService(database);

        var todayResult = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);
        var laterResult = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 5),
            TimeZoneInfo.Utc);
        var afterActivity = await ReadActivityAsync(database, activityId);
        var afterGoal = await database.Goals.GetByUserIdAsync(user.UserId);

        Assert.True(todayResult.IsSuccess);
        Assert.True(laterResult.IsSuccess);
        Assert.Equal(120, todayResult.Value!.TotalCalories);
        Assert.Equal(0, laterResult.Value!.TotalCalories);
        Assert.Equal(1, await CountActivityRowsAsync(database));
        Assert.Equal(1, await CountGoalRowsAsync(database));
        Assert.NotNull(beforeActivity);
        Assert.NotNull(afterActivity);
        Assert.Equal(beforeActivity!.CaloriesBurned, afterActivity!.CaloriesBurned);
        Assert.Equal(beforeActivity.RecordedAtUtc, afterActivity.RecordedAtUtc);
        Assert.NotNull(afterGoal);
        Assert.Equal(goal.GoalId, afterGoal!.GoalId);
        Assert.Equal(goal.TargetCalories, afterGoal.TargetCalories);
        Assert.Equal(goal.UpdatedAtUtc, afterGoal.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetTodayProgressAsync_IsDeterministicForTheSameInputs()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "deterministic-progress-user");
        await database.Goals.SaveAsync(new FitnessGoal(user.UserId, 300, BaseTime));
        await AddActivityAsync(
            database,
            user.UserId,
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero),
            120);
        var service = CreateService(database);

        var firstResult = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);
        var secondResult = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(firstResult.Value!.HasGoal, secondResult.Value!.HasGoal);
        Assert.Equal(firstResult.Value.TargetCalories, secondResult.Value.TargetCalories);
        Assert.Equal(firstResult.Value.TotalCalories, secondResult.Value.TotalCalories);
        Assert.Equal(firstResult.Value.RemainingCalories, secondResult.Value.RemainingCalories);
        Assert.Equal(firstResult.Value.ProgressPercentage, secondResult.Value.ProgressPercentage);
        Assert.Equal(firstResult.Value.IsGoalAchieved, secondResult.Value.IsGoalAchieved);
        Assert.Equal(firstResult.Value.StatusMessage, secondResult.Value.StatusMessage);
        Assert.Equal(1, await CountActivityRowsAsync(database));
        Assert.Equal(1, await CountGoalRowsAsync(database));
    }

    [Fact]
    public async Task GetTodayProgressAsync_PreservesGoalRepositoryFailures()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "progress-goal-table-user");
        await DropTableAsync(database, "FitnessGoals");
        var service = CreateService(database);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetTodayProgressAsync(
                user,
                new DateOnly(2026, 8, 4),
                TimeZoneInfo.Utc));

        Assert.IsType<SqliteException>(exception.InnerException);
    }

    [Fact]
    public async Task GetTodayProgressAsync_PreservesActivityRepositoryFailures()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "progress-activity-table-user");
        await database.Goals.SaveAsync(new FitnessGoal(user.UserId, 300, BaseTime));
        await DropTableAsync(database, "ActivityRecords");
        var service = CreateService(database);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetTodayProgressAsync(
                user,
                new DateOnly(2026, 8, 4),
                TimeZoneInfo.Utc));

        Assert.IsType<SqliteException>(exception.InnerException);
    }

    private static ProgressService CreateService(RepositoryTestDatabase database)
    {
        return new ProgressService(database.Goals, database.Activities);
    }

    private static async Task<ProgressSummary> GetProgressWithGoalAndCaloriesAsync(
        int targetCalories,
        double calories)
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "goal-progress-user");
        await database.Goals.SaveAsync(new FitnessGoal(user.UserId, targetCalories, BaseTime));
        await AddActivityAsync(
            database,
            user.UserId,
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero),
            calories);
        var service = CreateService(database);

        var result = await service.GetTodayProgressAsync(
            user,
            new DateOnly(2026, 8, 4),
            TimeZoneInfo.Utc);

        Assert.True(result.IsSuccess);
        return result.Value!;
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

    private static async Task<long> AddActivityAsync(
        RepositoryTestDatabase database,
        long userId,
        DateTimeOffset recordedAtUtc,
        double calories)
    {
        return await database.Activities.AddAsync(new ActivityRecord(
            userId,
            ActivityType.Walking,
            1,
            2,
            3,
            calories,
            recordedAtUtc));
    }

    private static async Task<long> CountActivityRowsAsync(RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ActivityRecords;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountGoalRowsAsync(RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM FitnessGoals;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<ActivitySnapshot?> ReadActivityAsync(
        RepositoryTestDatabase database,
        long activityRecordId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ActivityRecordId, CaloriesBurned, RecordedAtUtc
            FROM ActivityRecords
            WHERE ActivityRecordId = $activityRecordId;
            """;
        command.Parameters.Add(RepositoryTestDatabase.Parameter("$activityRecordId", activityRecordId));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ActivitySnapshot(
            reader.GetInt64(0),
            reader.GetDouble(1),
            DateTimeOffset.Parse(
                reader.GetString(2),
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

    private static TimeZoneInfo CreateUtcPlusSixThirtyTimeZone()
    {
        return TimeZoneInfo.CreateCustomTimeZone(
            "FitTrack UTC+06:30",
            TimeSpan.FromMinutes(390),
            "UTC+06:30",
            "UTC+06:30");
    }

    private static TimeZoneInfo GetNewYorkTimeZone()
    {
        foreach (var timeZoneId in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException(
            "No cross-platform New York daylight-saving time zone is available.");
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) ConvertBoundaries(
        DateOnly localDate,
        TimeZoneInfo timeZone)
    {
        var localStart = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = localStart.AddDays(1);
        return (
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone)),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone)));
    }

    private sealed record ActivitySnapshot(
        long ActivityRecordId,
        double CaloriesBurned,
        DateTimeOffset RecordedAtUtc);
}
