using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FitTrack.Calculators;
using FitTrack.Common;
using FitTrack.Models;
using FitTrack.Repositories;
using FitTrack.Services;
using FitTrack.Tests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FitTrack.Tests.Services;

public sealed class ActivityServiceTests
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 8, 3, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetActivityDefinitions_ReturnsTheApprovedImmutableDefinitions()
    {
        var service = CreateServiceWithoutDatabase();
        var definitions = service.GetActivityDefinitions();

        var expectedDefinitions = new[]
        {
            new DefinitionExpectation(
                ActivityType.Walking,
                "Walking",
                new[]
                {
                    new MetricExpectation("Steps", "steps", 1, 100000, true),
                    new MetricExpectation("Distance", "km", 0.1, 100, false),
                    new MetricExpectation("Duration", "minutes", 1, 720, false)
                }),
            new DefinitionExpectation(
                ActivityType.Swimming,
                "Swimming",
                new[]
                {
                    new MetricExpectation("Laps", "laps", 1, 400, true),
                    new MetricExpectation("Duration", "minutes", 1, 300, false),
                    new MetricExpectation("Average heart rate", "bpm", 40, 220, false)
                }),
            new DefinitionExpectation(
                ActivityType.Running,
                "Running",
                new[]
                {
                    new MetricExpectation("Distance", "km", 0.1, 100, false),
                    new MetricExpectation("Duration", "minutes", 1, 720, false),
                    new MetricExpectation("Average pace", "min/km", 3, 15, false)
                }),
            new DefinitionExpectation(
                ActivityType.Cycling,
                "Cycling",
                new[]
                {
                    new MetricExpectation("Distance", "km", 0.1, 300, false),
                    new MetricExpectation("Duration", "minutes", 1, 720, false),
                    new MetricExpectation("Average speed", "km/h", 3, 60, false)
                }),
            new DefinitionExpectation(
                ActivityType.StationaryRowing,
                "Stationary Rowing",
                new[]
                {
                    new MetricExpectation("Duration", "minutes", 1, 180, false),
                    new MetricExpectation("Average power", "watts", 20, 400, false),
                    new MetricExpectation("Stroke rate", "strokes/min", 10, 50, false)
                }),
            new DefinitionExpectation(
                ActivityType.StrengthTraining,
                "Strength Training",
                new[]
                {
                    new MetricExpectation("Duration", "minutes", 1, 180, false),
                    new MetricExpectation("Total sets", "sets", 1, 50, true),
                    new MetricExpectation("Effort level", "level", 1, 3, true)
                })
        };

        Assert.Equal(expectedDefinitions.Length, definitions.Count);
        Assert.Equal(
            expectedDefinitions.Select(expected => expected.ActivityType),
            definitions.Select(definition => definition.ActivityType));
        Assert.Equal(
            expectedDefinitions.Select(expected => expected.ActivityType).Distinct().Count(),
            definitions.Select(definition => definition.ActivityType).Distinct().Count());

        for (var definitionIndex = 0; definitionIndex < expectedDefinitions.Length; definitionIndex++)
        {
            var expected = expectedDefinitions[definitionIndex];
            var actual = definitions[definitionIndex];

            Assert.Equal(expected.DisplayName, actual.DisplayName);
            Assert.Equal(3, actual.Metrics.Count);

            for (var metricIndex = 0; metricIndex < expected.Metrics.Length; metricIndex++)
            {
                var expectedMetric = expected.Metrics[metricIndex];
                var actualMetric = actual.Metrics[metricIndex];

                Assert.Equal(expectedMetric.Label, actualMetric.Label);
                Assert.Equal(expectedMetric.Unit, actualMetric.Unit);
                Assert.Equal(expectedMetric.Minimum, actualMetric.Minimum);
                Assert.Equal(expectedMetric.Maximum, actualMetric.Maximum);
                Assert.Equal(expectedMetric.WholeNumberOnly, actualMetric.WholeNumberOnly);
            }
        }

        Assert.Same(definitions, service.GetActivityDefinitions());
        var readOnlyDefinitions = Assert.IsAssignableFrom<IList<ActivityDefinition>>(definitions);
        Assert.True(readOnlyDefinitions.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => readOnlyDefinitions.Add(definitions[0]));
    }

    [Theory]
    [InlineData(ActivityType.Walking)]
    [InlineData(ActivityType.Swimming)]
    [InlineData(ActivityType.Running)]
    [InlineData(ActivityType.Cycling)]
    [InlineData(ActivityType.StationaryRowing)]
    [InlineData(ActivityType.StrengthTraining)]
    public void GetActivityDefinition_ReturnsTheMatchingDefinition(ActivityType activityType)
    {
        var service = CreateServiceWithoutDatabase();

        var result = service.GetActivityDefinition(activityType);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(activityType, result.Value!.ActivityType);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void GetActivityDefinition_ReturnsAControlledFailureForUnsupportedActivity()
    {
        var service = CreateServiceWithoutDatabase();

        var result = service.GetActivityDefinition((ActivityType)999);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal("The selected activity is not supported.", result.ErrorMessage);
    }

    [Theory]
    [MemberData(nameof(SuccessfulActivityCases))]
    public async Task RecordActivityAsync_PersistsEachApprovedActivityInMetricOrder(
        ActivityType activityType,
        double metric1Value,
        double metric2Value,
        double metric3Value,
        double expectedCalories)
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "activity-user");
        var service = CreateService(database);

        var result = await service.RecordActivityAsync(
            user,
            activityType,
            metric1Value,
            metric2Value,
            metric3Value,
            BaseTime);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.ActivityRecordId > 0);
        Assert.Equal(user.UserId, result.Value.UserId);
        Assert.Equal(activityType, result.Value.ActivityType);
        Assert.Equal(metric1Value, result.Value.Metric1Value);
        Assert.Equal(metric2Value, result.Value.Metric2Value);
        Assert.Equal(metric3Value, result.Value.Metric3Value);
        Assert.Equal(expectedCalories, result.Value.CaloriesBurned);
        Assert.Equal(BaseTime, result.Value.RecordedAtUtc);
        Assert.Equal(1, await CountActivityRowsAsync(database));

        var storedRow = await ReadActivityRowAsync(database, result.Value.ActivityRecordId);
        Assert.NotNull(storedRow);
        Assert.Equal(result.Value.ActivityRecordId, storedRow!.ActivityRecordId);
        Assert.Equal(result.Value.UserId, storedRow.UserId);
        Assert.Equal(result.Value.ActivityType.ToString(), storedRow.ActivityType);
        Assert.Equal(result.Value.Metric1Value, storedRow.Metric1Value);
        Assert.Equal(result.Value.Metric2Value, storedRow.Metric2Value);
        Assert.Equal(result.Value.Metric3Value, storedRow.Metric3Value);
        Assert.Equal(result.Value.CaloriesBurned, storedRow.CaloriesBurned);
        Assert.Equal(result.Value.RecordedAtUtc, storedRow.RecordedAtUtc);
    }

    [Fact]
    public async Task RecordActivityAsync_RejectsANullUserWithoutCallingACalculator()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var countingCalculator = new CountingCalculator(ActivityType.Walking, 123.45);
        var service = CreateService(database, countingCalculator);

        var result = await service.RecordActivityAsync(
            null,
            ActivityType.Walking,
            5000,
            4,
            60,
            BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal("A registered user is required to record an activity.", result.ErrorMessage);
        Assert.Null(result.Value);
        Assert.Equal(0, countingCalculator.CallCount);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RecordActivityAsync_RejectsNonPositiveUserIdsWithoutInsertion(long userId)
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var countingCalculator = new CountingCalculator(ActivityType.Walking, 123.45);
        var service = CreateService(database, countingCalculator);
        var user = userId == 0
            ? new User("unsaved-user", "fake-hash", BaseTime)
            : new User(userId, "invalid-user", "fake-hash", 0, null, BaseTime);

        var result = await service.RecordActivityAsync(
            user,
            ActivityType.Walking,
            5000,
            4,
            60,
            BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal("A registered user is required to record an activity.", result.ErrorMessage);
        Assert.Equal(0, countingCalculator.CallCount);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Fact]
    public async Task RecordActivityAsync_RejectsUnsupportedActivityWithoutCallingACalculator()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var countingCalculator = new CountingCalculator(ActivityType.Walking, 123.45);
        var service = CreateService(database, countingCalculator);
        var user = await CreateUserAsync(database, "unsupported-user");

        var result = await service.RecordActivityAsync(
            user,
            (ActivityType)999,
            5000,
            4,
            60,
            BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal("The selected activity is not supported.", result.ErrorMessage);
        Assert.Equal(0, countingCalculator.CallCount);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Theory]
    [MemberData(nameof(InvalidMetricCases))]
    public async Task RecordActivityAsync_TranslatesInvalidMetricsAndDoesNotInsert(
        ActivityType activityType,
        double metric1Value,
        double metric2Value,
        double metric3Value,
        string expectedMessage)
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = CreateService(database);
        var user = await CreateUserAsync(database, "invalid-metric-user");

        var result = await service.RecordActivityAsync(
            user,
            activityType,
            metric1Value,
            metric2Value,
            metric3Value,
            BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Theory]
    [MemberData(nameof(WholeNumberCases))]
    public async Task RecordActivityAsync_TranslatesWholeNumberFailures(
        ActivityType activityType,
        double metric1Value,
        double metric2Value,
        double metric3Value,
        string expectedMessage)
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = CreateService(database);
        var user = await CreateUserAsync(database, "whole-number-user");

        var result = await service.RecordActivityAsync(
            user,
            activityType,
            metric1Value,
            metric2Value,
            metric3Value,
            BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Fact]
    public async Task RecordActivityAsync_TranslatesRunningPaceInconsistency()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = CreateService(database);
        var user = await CreateUserAsync(database, "running-inconsistent-user");

        var result = await service.RecordActivityAsync(
            user,
            ActivityType.Running,
            5,
            30,
            6.61,
            BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal("The activity metric values are inconsistent.", result.ErrorMessage);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Fact]
    public async Task RecordActivityAsync_TranslatesCyclingSpeedInconsistency()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = CreateService(database);
        var user = await CreateUserAsync(database, "cycling-inconsistent-user");

        var result = await service.RecordActivityAsync(
            user,
            ActivityType.Cycling,
            20,
            60,
            22.01,
            BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal("The activity metric values are inconsistent.", result.ErrorMessage);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Fact]
    public async Task RecordActivityAsync_ConvertsCalculatorOutOfRangeExceptions()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var throwingCalculator = new ThrowingCalculator(
            ActivityType.Walking,
            new ArgumentOutOfRangeException("metric2Value", "invalid test metric"));
        var service = CreateService(database, throwingCalculator);
        var user = await CreateUserAsync(database, "throwing-range-user");

        var result = await service.RecordActivityAsync(
            user,
            ActivityType.Walking,
            5000,
            4,
            60,
            BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal("Distance is outside the allowed range.", result.ErrorMessage);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Fact]
    public async Task RecordActivityAsync_ConvertsUnassociatedCalculatorArgumentExceptions()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var throwingCalculator = new ThrowingCalculator(
            ActivityType.Walking,
            new ArgumentException("internal calculator detail"));
        var service = CreateService(database, throwingCalculator);
        var user = await CreateUserAsync(database, "throwing-argument-user");

        var result = await service.RecordActivityAsync(
            user,
            ActivityType.Walking,
            5000,
            4,
            60,
            BaseTime);

        Assert.False(result.IsSuccess);
        Assert.Equal("The activity metric values are inconsistent.", result.ErrorMessage);
        Assert.DoesNotContain("internal calculator detail", result.ErrorMessage);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Fact]
    public void Constructor_RejectsDuplicateCalculatorAssociations()
    {
        var calculators = CreateRealCalculators().ToList();
        calculators.Add(new FixedCalculator(ActivityType.Walking, 1));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ActivityService(CreateRepositoryWithoutDatabase(), calculators));

        Assert.Equal("More than one calculator is registered for Walking.", exception.Message);
    }

    [Fact]
    public void Constructor_RejectsMissingCalculatorAssociations()
    {
        var calculators = CreateRealCalculators()
            .Where(calculator => calculator.ActivityType != ActivityType.Running)
            .ToArray();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ActivityService(CreateRepositoryWithoutDatabase(), calculators));

        Assert.Equal("A calculator is not registered for Running.", exception.Message);
    }

    [Fact]
    public void Constructor_RejectsUnsupportedCalculatorAssociations()
    {
        var calculators = CreateRealCalculators()
            .Append(new FixedCalculator((ActivityType)999, 1));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ActivityService(CreateRepositoryWithoutDatabase(), calculators));

        Assert.Contains("is not supported", exception.Message);
    }

    [Fact]
    public void Constructor_RejectsNullCalculatorCollection()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ActivityService(CreateRepositoryWithoutDatabase(), null!));
    }

    [Fact]
    public void Constructor_RejectsNullCalculatorEntries()
    {
        var calculators = CreateRealCalculators();
        calculators[0] = null!;

        Assert.Throws<ArgumentNullException>(
            () => new ActivityService(CreateRepositoryWithoutDatabase(), calculators));
    }

    [Fact]
    public async Task RecordActivityAsync_CallsTheSelectedCalculatorOnceAndStoresItsValue()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var countingCalculator = new CountingCalculator(ActivityType.Walking, 123.45);
        var service = CreateService(database, countingCalculator);
        var user = await CreateUserAsync(database, "counting-user");

        var result = await service.RecordActivityAsync(
            user,
            ActivityType.Walking,
            5000,
            4,
            60,
            BaseTime);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, countingCalculator.CallCount);
        Assert.NotNull(result.Value);
        Assert.Equal(123.45, result.Value!.CaloriesBurned);
        var storedRow = await ReadActivityRowAsync(database, result.Value.ActivityRecordId);
        Assert.NotNull(storedRow);
        Assert.Equal(123.45, storedRow!.CaloriesBurned);
        Assert.Equal(1, await CountActivityRowsAsync(database));
    }

    [Fact]
    public async Task RecordActivityAsync_PropagatesForeignKeyRepositoryFailures()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var service = CreateService(database);
        var missingUser = new User(
            99999,
            "missing-user",
            "fake-hash",
            0,
            null,
            BaseTime);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordActivityAsync(
                missingUser,
                ActivityType.Walking,
                5000,
                4,
                60,
                BaseTime));

        Assert.IsType<SqliteException>(exception.InnerException);
        var sqliteException = (SqliteException)exception.InnerException!;
        Assert.Equal(19, sqliteException.SqliteErrorCode & 0xFF);
        Assert.Equal(0, await CountActivityRowsAsync(database));
    }

    [Fact]
    public async Task RecordActivityAsync_PropagatesUnexpectedRepositoryWriteFailures()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "missing-table-user");
        await using (var connection = await database.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DROP TABLE ActivityRecords;";
            await command.ExecuteNonQueryAsync();
        }

        var service = CreateService(database);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordActivityAsync(
                user,
                ActivityType.Walking,
                5000,
                4,
                60,
                BaseTime));

        Assert.IsType<SqliteException>(exception.InnerException);
    }

    [Fact]
    public async Task RecordActivityAsync_NormalizesUtcAndNonUtcTimestampsWithoutChangingTheInstant()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "timestamp-user");
        var service = CreateService(database);
        var utcInput = BaseTime.AddTicks(1234);
        var offsetInput = new DateTimeOffset(
            2026,
            8,
            3,
            18,
            30,
            0,
            TimeSpan.FromHours(6.5)).AddTicks(5678);

        var utcResult = await service.RecordActivityAsync(
            user,
            ActivityType.Walking,
            5000,
            4,
            60,
            utcInput);
        var offsetResult = await service.RecordActivityAsync(
            user,
            ActivityType.Walking,
            5000,
            4,
            60,
            offsetInput);

        Assert.True(utcResult.IsSuccess);
        Assert.True(offsetResult.IsSuccess);
        Assert.Equal(utcInput.ToUniversalTime(), utcResult.Value!.RecordedAtUtc);
        Assert.Equal(offsetInput.ToUniversalTime(), offsetResult.Value!.RecordedAtUtc);

        var storedUtc = await ReadActivityRowAsync(database, utcResult.Value.ActivityRecordId);
        var storedOffset = await ReadActivityRowAsync(database, offsetResult.Value.ActivityRecordId);
        Assert.NotNull(storedUtc);
        Assert.NotNull(storedOffset);
        Assert.Equal(utcInput.ToUniversalTime(), storedUtc!.RecordedAtUtc);
        Assert.Equal(offsetInput.ToUniversalTime(), storedOffset!.RecordedAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero).AddTicks(5678), storedOffset.RecordedAtUtc);
    }

    [Fact]
    public async Task RecordActivityAsync_IsDeterministicForIdenticalMetrics()
    {
        await using var database = await RepositoryTestDatabase.CreateAsync();
        var user = await CreateUserAsync(database, "deterministic-user");
        var service = CreateService(database);

        var firstResult = await service.RecordActivityAsync(
            user,
            ActivityType.Running,
            5,
            30,
            6,
            BaseTime);
        var secondResult = await service.RecordActivityAsync(
            user,
            ActivityType.Running,
            5,
            30,
            6,
            BaseTime.AddMinutes(1));

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.NotEqual(firstResult.Value!.ActivityRecordId, secondResult.Value!.ActivityRecordId);
        Assert.Equal(firstResult.Value.CaloriesBurned, secondResult.Value.CaloriesBurned);
        Assert.Equal(2, await CountActivityRowsAsync(database));
    }

    public static IEnumerable<object[]> SuccessfulActivityCases()
    {
        yield return new object[] { ActivityType.Walking, 5000.0, 4.0, 60.0, 245.0 };
        yield return new object[] { ActivityType.Swimming, 80.0, 40.0, 140.0, 373.33 };
        yield return new object[] { ActivityType.Running, 5.0, 30.0, 6.0, 325.50 };
        yield return new object[] { ActivityType.Cycling, 20.0, 60.0, 20.0, 560.0 };
        yield return new object[] { ActivityType.StationaryRowing, 30.0, 150.0, 25.0, 385.0 };
        yield return new object[] { ActivityType.StrengthTraining, 45.0, 12.0, 2.0, 262.50 };
    }

    public static IEnumerable<object[]> InvalidMetricCases()
    {
        var validCases = new[]
        {
            new MetricCase(ActivityType.Walking, 5000, 4, 60, new[] { "Steps", "Distance", "Duration" }),
            new MetricCase(ActivityType.Swimming, 80, 40, 140, new[] { "Laps", "Duration", "Average heart rate" }),
            new MetricCase(ActivityType.Running, 5, 30, 6, new[] { "Distance", "Duration", "Average pace" }),
            new MetricCase(ActivityType.Cycling, 20, 60, 20, new[] { "Distance", "Duration", "Average speed" }),
            new MetricCase(ActivityType.StationaryRowing, 30, 150, 25, new[] { "Duration", "Average power", "Stroke rate" }),
            new MetricCase(ActivityType.StrengthTraining, 45, 12, 2, new[] { "Duration", "Total sets", "Effort level" })
        };
        var invalidValues = new[]
        {
            0.0,
            -1.0,
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity
        };

        foreach (var validCase in validCases)
        {
            var metrics = new[] { validCase.Metric1, validCase.Metric2, validCase.Metric3 };
            foreach (var invalidValue in invalidValues)
            {
                for (var metricIndex = 0; metricIndex < metrics.Length; metricIndex++)
                {
                    var invalidMetrics = (double[])metrics.Clone();
                    invalidMetrics[metricIndex] = invalidValue;
                    yield return new object[]
                    {
                        validCase.ActivityType,
                        invalidMetrics[0],
                        invalidMetrics[1],
                        invalidMetrics[2],
                        $"{validCase.MetricLabels[metricIndex]} is outside the allowed range."
                    };
                }
            }
        }
    }

    public static IEnumerable<object[]> WholeNumberCases()
    {
        yield return new object[]
        {
            ActivityType.Walking,
            5000.5,
            4.0,
            60.0,
            "Steps is invalid."
        };
        yield return new object[]
        {
            ActivityType.Swimming,
            20.5,
            40.0,
            140.0,
            "Laps is invalid."
        };
        yield return new object[]
        {
            ActivityType.StrengthTraining,
            45.0,
            10.5,
            2.0,
            "Total sets is invalid."
        };
        yield return new object[]
        {
            ActivityType.StrengthTraining,
            45.0,
            12.0,
            2.5,
            "Effort level is invalid."
        };
    }

    private static ActivityService CreateService(RepositoryTestDatabase database)
    {
        return new ActivityService(database.Activities, CreateRealCalculators());
    }

    private static ActivityService CreateService(
        RepositoryTestDatabase database,
        IActivityCalculator replacementCalculator)
    {
        var calculators = CreateRealCalculators();
        calculators[Array.FindIndex(
            calculators,
            calculator => calculator.ActivityType == replacementCalculator.ActivityType)] = replacementCalculator;
        return new ActivityService(database.Activities, calculators);
    }

    private static ActivityService CreateServiceWithoutDatabase()
    {
        return new ActivityService(CreateRepositoryWithoutDatabase(), CreateRealCalculators());
    }

    private static ActivityRepository CreateRepositoryWithoutDatabase()
    {
        return new ActivityRepository("Data Source=:memory:");
    }

    private static IActivityCalculator[] CreateRealCalculators()
    {
        return new IActivityCalculator[]
        {
            new WalkingCalculator(),
            new SwimmingCalculator(),
            new RunningCalculator(),
            new CyclingCalculator(),
            new StationaryRowingCalculator(),
            new StrengthTrainingCalculator()
        };
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

    private static async Task<long> CountActivityRowsAsync(RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ActivityRecords;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<StoredActivityRow?> ReadActivityRowAsync(
        RepositoryTestDatabase database,
        long activityRecordId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ActivityRecordId, UserId, ActivityType, Metric1Value, Metric2Value,
                   Metric3Value, CaloriesBurned, RecordedAtUtc
            FROM ActivityRecords
            WHERE ActivityRecordId = $activityRecordId;
            """;
        command.Parameters.Add(RepositoryTestDatabase.Parameter("$activityRecordId", activityRecordId));

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new StoredActivityRow(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetDouble(3),
            reader.GetDouble(4),
            reader.GetDouble(5),
            reader.GetDouble(6),
            DateTimeOffset.Parse(
                reader.GetString(7),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal));
    }

    private sealed record DefinitionExpectation(
        ActivityType ActivityType,
        string DisplayName,
        MetricExpectation[] Metrics);

    private sealed record MetricExpectation(
        string Label,
        string Unit,
        double Minimum,
        double Maximum,
        bool WholeNumberOnly);

    private sealed record MetricCase(
        ActivityType ActivityType,
        double Metric1,
        double Metric2,
        double Metric3,
        string[] MetricLabels);

    private sealed record StoredActivityRow(
        long ActivityRecordId,
        long UserId,
        string ActivityType,
        double Metric1Value,
        double Metric2Value,
        double Metric3Value,
        double CaloriesBurned,
        DateTimeOffset RecordedAtUtc);

    private sealed class CountingCalculator : IActivityCalculator
    {
        private readonly double _calories;

        public CountingCalculator(ActivityType activityType, double calories)
        {
            ActivityType = activityType;
            _calories = calories;
        }

        public ActivityType ActivityType { get; }

        public int CallCount { get; private set; }

        public double CalculateCalories(double metric1Value, double metric2Value, double metric3Value)
        {
            CallCount++;
            return _calories;
        }
    }

    private sealed class FixedCalculator : IActivityCalculator
    {
        private readonly double _calories;

        public FixedCalculator(ActivityType activityType, double calories)
        {
            ActivityType = activityType;
            _calories = calories;
        }

        public ActivityType ActivityType { get; }

        public double CalculateCalories(double metric1Value, double metric2Value, double metric3Value)
        {
            return _calories;
        }
    }

    private sealed class ThrowingCalculator : IActivityCalculator
    {
        private readonly Exception _exception;

        public ThrowingCalculator(ActivityType activityType, Exception exception)
        {
            ActivityType = activityType;
            _exception = exception;
        }

        public ActivityType ActivityType { get; }

        public double CalculateCalories(double metric1Value, double metric2Value, double metric3Value)
        {
            throw _exception;
        }
    }
}
