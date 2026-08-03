using System;
using System.Globalization;
using System.Threading.Tasks;
using FitnessApp.Common;
using FitnessApp.Models;
using FitnessApp.Repositories;
using FitnessApp.Services;
using FitnessApp.Tests.Data;
using FitnessApp.ViewModels;
using Xunit;

namespace FitnessApp.Tests.ViewModels;

public sealed class GoalViewModelTests
{
    private const string Password = "FitnessPass1";
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 3, 18, 30, 0, TimeSpan.FromHours(6.5));

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var authenticationService = new AuthenticationService(
            new UserRepository("Data Source=:memory:"));
        var goalService = new GoalService(new GoalRepository("Data Source=:memory:"));
        var navigationService = new NavigationService();

        Assert.Throws<ArgumentNullException>(() => new GoalViewModel(
            null!,
            goalService,
            navigationService,
            static () => FixedUtcNow));
        Assert.Throws<ArgumentNullException>(() => new GoalViewModel(
            authenticationService,
            null!,
            navigationService,
            static () => FixedUtcNow));
        Assert.Throws<ArgumentNullException>(() => new GoalViewModel(
            authenticationService,
            goalService,
            null!,
            static () => FixedUtcNow));
        Assert.Throws<ArgumentNullException>(() => new GoalViewModel(
            authenticationService,
            goalService,
            navigationService,
            null!));
    }

    [Fact]
    public async Task LoadGoalAsync_NoGoalIsASuccessfulLoadedState()
    {
        await using var fixture = await GoalFixture.CreateAsync();

        await fixture.LoadAsync();

        Assert.Equal(fixture.User.Username, fixture.Goal.Username);
        Assert.True(fixture.Goal.HasLoaded);
        Assert.False(fixture.Goal.HasExistingGoal);
        Assert.Equal(0, fixture.Goal.ExistingTargetCalories);
        Assert.Empty(fixture.Goal.GoalInput);
        Assert.Equal("No daily calorie goal has been set.", fixture.Goal.StatusMessage);
        Assert.Empty(fixture.Goal.ErrorMessage);
        Assert.True(fixture.Goal.ShowNoGoalPrompt);
        Assert.False(fixture.Goal.ShowExistingGoal);
        Assert.False(fixture.Goal.IsBusy);
    }

    [Fact]
    public async Task LoadGoalAsync_ExistingGoalUsesInvariantIntegerInput()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            2300,
            FixedUtcNow));

        await fixture.LoadAsync();

        Assert.True(fixture.Goal.HasLoaded);
        Assert.True(fixture.Goal.HasExistingGoal);
        Assert.Equal(2300, fixture.Goal.ExistingTargetCalories);
        Assert.Equal("2300", fixture.Goal.GoalInput);
        Assert.Empty(fixture.Goal.StatusMessage);
        Assert.Empty(fixture.Goal.ErrorMessage);
        Assert.True(fixture.Goal.ShowExistingGoal);
        Assert.False(fixture.Goal.ShowNoGoalPrompt);
    }

    [Fact]
    public async Task LoadGoalAsync_LaterTechnicalFailureRetainsPreviousGoalState()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            2300,
            FixedUtcNow));
        await fixture.LoadAsync();
        await RenameGoalTableAsync(fixture.Database);

        var exception = await Record.ExceptionAsync(() => fixture.LoadAsync());

        Assert.Null(exception);
        Assert.Equal(AppRoute.Goal, fixture.NavigationService.CurrentRoute);
        Assert.Equal("Unable to load your goal right now.", fixture.Goal.ErrorMessage);
        Assert.Equal("2300", fixture.Goal.GoalInput);
        Assert.Equal(2300, fixture.Goal.ExistingTargetCalories);
        Assert.True(fixture.Goal.HasExistingGoal);
        Assert.True(fixture.Goal.HasLoaded);
        Assert.False(fixture.Goal.IsBusy);
        Assert.DoesNotContain("FitnessGoals", fixture.Goal.ErrorMessage);
        Assert.DoesNotContain("SQL", fixture.Goal.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fitnessapp.db", fixture.Goal.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadGoalAsync_InitialTechnicalFailureDoesNotMarkLoaded()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        await RenameGoalTableAsync(fixture.Database);

        var exception = await Record.ExceptionAsync(() => fixture.LoadAsync());

        Assert.Null(exception);
        Assert.Equal(AppRoute.Goal, fixture.NavigationService.CurrentRoute);
        Assert.Equal("Unable to load your goal right now.", fixture.Goal.ErrorMessage);
        Assert.False(fixture.Goal.HasLoaded);
        Assert.False(fixture.Goal.IsBusy);
    }

    [Fact]
    public async Task LoadGoalAsync_MissingSessionClearsStateAndReturnsToLogin()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            1800,
            FixedUtcNow));
        await fixture.LoadAsync();
        fixture.AuthenticationService.Logout();

        var exception = await Record.ExceptionAsync(() => fixture.LoadAsync());

        Assert.Null(exception);
        Assert.Equal(AppRoute.Login, fixture.NavigationService.CurrentRoute);
        Assert.Empty(fixture.Goal.Username);
        Assert.Empty(fixture.Goal.GoalInput);
        Assert.Equal(0, fixture.Goal.ExistingTargetCalories);
        Assert.False(fixture.Goal.HasExistingGoal);
        Assert.False(fixture.Goal.HasLoaded);
        Assert.Empty(fixture.Goal.ErrorMessage);
        Assert.Empty(fixture.Goal.StatusMessage);
    }

    [Fact]
    public async Task LoadGoalAsync_MissingSessionDoesNotAccessGoalPersistence()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        await RenameGoalTableAsync(fixture.Database);
        fixture.AuthenticationService.Logout();

        var exception = await Record.ExceptionAsync(() => fixture.LoadAsync());

        Assert.Null(exception);
        Assert.Equal(AppRoute.Login, fixture.NavigationService.CurrentRoute);
        Assert.Empty(fixture.Goal.ErrorMessage);
        Assert.False(fixture.Goal.HasLoaded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveGoalAsync_RejectsMissingInput(string input)
    {
        await using var fixture = await GoalFixture.CreateAsync();
        fixture.Goal.GoalInput = input;

        await fixture.SaveAsync();

        Assert.Equal(AppRoute.Goal, fixture.NavigationService.CurrentRoute);
        Assert.Equal(input, fixture.Goal.GoalInput);
        Assert.Equal("Enter a daily calorie goal.", fixture.Goal.ErrorMessage);
        Assert.Equal(0, await CountGoalsAsync(fixture.Database, fixture.User.UserId));
        Assert.False(fixture.Goal.IsBusy);
    }

    [Theory]
    [InlineData(" 100")]
    [InlineData("100 ")]
    [InlineData(" 100 ")]
    public async Task SaveGoalAsync_RejectsLeadingAndTrailingWhitespace(string input)
    {
        await using var fixture = await GoalFixture.CreateAsync();
        fixture.Goal.GoalInput = input;

        await fixture.SaveAsync();

        Assert.Equal(AppRoute.Goal, fixture.NavigationService.CurrentRoute);
        Assert.Equal(input, fixture.Goal.GoalInput);
        Assert.Equal(
            "Enter the goal without leading or trailing spaces.",
            fixture.Goal.ErrorMessage);
        Assert.Equal(0, await CountGoalsAsync(fixture.Database, fixture.User.UserId));
        Assert.False(fixture.Goal.IsBusy);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("100.5")]
    [InlineData("5,000")]
    [InlineData("1 000")]
    public async Task SaveGoalAsync_RejectsNonWholeNumberInput(string input)
    {
        await using var fixture = await GoalFixture.CreateAsync();
        fixture.Goal.GoalInput = input;

        await fixture.SaveAsync();

        Assert.Equal(AppRoute.Goal, fixture.NavigationService.CurrentRoute);
        Assert.Equal(input, fixture.Goal.GoalInput);
        Assert.Equal(
            "Goal must be a whole number from 1 to 5,000.",
            fixture.Goal.ErrorMessage);
        Assert.Equal(0, await CountGoalsAsync(fixture.Database, fixture.User.UserId));
        Assert.False(fixture.Goal.IsBusy);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("5001")]
    public async Task SaveGoalAsync_UsesGoalServiceRangeValidation(string input)
    {
        await using var fixture = await GoalFixture.CreateAsync();
        fixture.Goal.GoalInput = input;

        await fixture.SaveAsync();

        Assert.Equal(AppRoute.Goal, fixture.NavigationService.CurrentRoute);
        Assert.Equal(input, fixture.Goal.GoalInput);
        Assert.Equal(
            "Goal must be between 1 and 5,000 calories.",
            fixture.Goal.ErrorMessage);
        Assert.Equal(0, await CountGoalsAsync(fixture.Database, fixture.User.UserId));
        Assert.False(fixture.Goal.IsBusy);
        Assert.NotNull(fixture.AuthenticationService.CurrentUser);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("5000")]
    public async Task SaveGoalAsync_AcceptsTheApprovedMinimumAndMaximum(string input)
    {
        await using var fixture = await GoalFixture.CreateAsync();
        fixture.Goal.GoalInput = input;

        await fixture.SaveAsync();

        Assert.Equal(AppRoute.Dashboard, fixture.NavigationService.CurrentRoute);
        Assert.Equal(input, fixture.Goal.GoalInput);
        Assert.True(fixture.Goal.HasExistingGoal);
        Assert.True(fixture.Goal.HasLoaded);
        Assert.Equal(int.Parse(input, CultureInfo.InvariantCulture), fixture.Goal.ExistingTargetCalories);
        Assert.Equal(1, await CountGoalsAsync(fixture.Database, fixture.User.UserId));
        Assert.False(fixture.Goal.IsBusy);
    }

    [Fact]
    public async Task SaveGoalAsync_PassesTheInjectedClockToGoalService()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        fixture.Goal.GoalInput = "2500";

        await fixture.SaveAsync();

        var storedGoal = await ReadGoalAsync(fixture.Database, fixture.User.UserId);
        Assert.NotNull(storedGoal);
        Assert.Equal(FixedUtcNow.ToUniversalTime(), storedGoal!.UpdatedAtUtc);
    }

    [Fact]
    public async Task SaveGoalAsync_TechnicalFailureRetainsInputAndPreviousGoal()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        var savedGoal = await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            1800,
            FixedUtcNow));
        await fixture.LoadAsync();
        await RenameGoalTableAsync(fixture.Database);
        fixture.Goal.GoalInput = "2400";

        var exception = await Record.ExceptionAsync(() => fixture.SaveAsync());

        Assert.Null(exception);
        Assert.Equal(AppRoute.Goal, fixture.NavigationService.CurrentRoute);
        Assert.Equal("2400", fixture.Goal.GoalInput);
        Assert.Equal(1800, fixture.Goal.ExistingTargetCalories);
        Assert.True(savedGoal.GoalId > 0);
        Assert.Equal("Unable to save your goal right now.", fixture.Goal.ErrorMessage);
        Assert.True(fixture.Goal.HasLoaded);
        Assert.False(fixture.Goal.IsBusy);
        Assert.Equal(1800, await ReadBackupTargetAsync(fixture.Database, fixture.User.UserId));
    }

    [Fact]
    public async Task SaveGoalAsync_DuplicateExecutionOnlySavesOnce()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hookCount = 0;
        fixture.Goal.BeforeGoalSaveAsync = async () =>
        {
            hookCount++;
            entered.SetResult(true);
            await release.Task;
        };
        fixture.Goal.GoalInput = "2200";

        var firstSave = fixture.Goal.SaveGoalCommand.ExecuteAsync(null);
        await entered.Task;
        var secondSave = fixture.Goal.SaveGoalCommand.ExecuteAsync(null);
        release.SetResult(true);

        var exception = await Record.ExceptionAsync(async () =>
            await Task.WhenAll(firstSave, secondSave));

        Assert.Null(exception);
        Assert.Equal(1, hookCount);
        Assert.Equal(1, await CountGoalsAsync(fixture.Database, fixture.User.UserId));
        Assert.Equal(AppRoute.Dashboard, fixture.NavigationService.CurrentRoute);
        Assert.False(fixture.Goal.IsBusy);
    }

    [Fact]
    public async Task LoadGoalAsync_RepeatedActivationOnlyLoadsOnce()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            2200,
            FixedUtcNow));
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hookCount = 0;
        fixture.Goal.BeforeGoalLoadAsync = async () =>
        {
            hookCount++;
            entered.SetResult(true);
            await release.Task;
        };

        var firstLoad = fixture.Goal.LoadGoalCommand.ExecuteAsync(null);
        await entered.Task;
        var secondLoad = fixture.Goal.LoadGoalCommand.ExecuteAsync(null);
        release.SetResult(true);

        var exception = await Record.ExceptionAsync(async () =>
            await Task.WhenAll(firstLoad, secondLoad));

        Assert.Null(exception);
        Assert.Equal(1, hookCount);
        Assert.True(fixture.Goal.HasLoaded);
        Assert.Equal(2200, fixture.Goal.ExistingTargetCalories);
        Assert.False(fixture.Goal.IsBusy);
    }

    [Fact]
    public async Task LogoutCommandClearsStateWithoutDeletingTheStoredGoal()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        await fixture.Database.Goals.SaveAsync(new FitnessGoal(
            fixture.User.UserId,
            1800,
            FixedUtcNow));
        await fixture.LoadAsync();

        fixture.Goal.LogoutCommand.Execute(null);

        Assert.Null(fixture.AuthenticationService.CurrentUser);
        Assert.Equal(AppRoute.Login, fixture.NavigationService.CurrentRoute);
        Assert.Empty(fixture.Goal.Username);
        Assert.Empty(fixture.Goal.GoalInput);
        Assert.Equal(0, fixture.Goal.ExistingTargetCalories);
        Assert.False(fixture.Goal.HasExistingGoal);
        Assert.False(fixture.Goal.HasLoaded);
        Assert.Equal(1, await CountGoalsAsync(fixture.Database, fixture.User.UserId));
    }

    [Fact]
    public async Task BackToDashboardCommandNavigatesWithoutSaving()
    {
        await using var fixture = await GoalFixture.CreateAsync();
        fixture.Goal.GoalInput = "1900";

        fixture.Goal.BackToDashboardCommand.Execute(null);

        Assert.Equal(AppRoute.Dashboard, fixture.NavigationService.CurrentRoute);
        Assert.NotNull(fixture.AuthenticationService.CurrentUser);
        Assert.Equal(0, await CountGoalsAsync(fixture.Database, fixture.User.UserId));
    }

    [Fact]
    public async Task ParsingIsInvariantAcrossCultures()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            await using var fixture = await GoalFixture.CreateAsync();

            fixture.Goal.GoalInput = "1000";
            await fixture.SaveAsync();
            Assert.Equal(AppRoute.Dashboard, fixture.NavigationService.CurrentRoute);

            fixture.NavigationService.Navigate(AppRoute.Goal);
            fixture.Goal.GoalInput = "100.5";
            await fixture.SaveAsync();
            Assert.Equal(
                "Goal must be a whole number from 1 to 5,000.",
                fixture.Goal.ErrorMessage);

            fixture.Goal.GoalInput = "1,000";
            await fixture.SaveAsync();
            Assert.Equal(
                "Goal must be a whole number from 1 to 5,000.",
                fixture.Goal.ErrorMessage);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static async Task RenameGoalTableAsync(RepositoryTestDatabase database)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE FitnessGoals RENAME TO FitnessGoals_Backup;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountGoalsAsync(
        RepositoryTestDatabase database,
        long userId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM FitnessGoals WHERE UserId = $userId;";
        command.Parameters.Add(RepositoryTestDatabase.Parameter("$userId", userId));
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture);
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

    private static async Task<double> ReadBackupTargetAsync(
        RepositoryTestDatabase database,
        long userId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT TargetCalories FROM FitnessGoals_Backup WHERE UserId = $userId;";
        command.Parameters.Add(RepositoryTestDatabase.Parameter("$userId", userId));
        return Convert.ToDouble(
            await command.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture);
    }

    private sealed class GoalFixture : IAsyncDisposable
    {
        private GoalFixture(
            RepositoryTestDatabase database,
            AuthenticationService authenticationService,
            NavigationService navigationService,
            GoalViewModel goal,
            User user)
        {
            Database = database;
            AuthenticationService = authenticationService;
            NavigationService = navigationService;
            Goal = goal;
            User = user;
        }

        public RepositoryTestDatabase Database { get; }

        public AuthenticationService AuthenticationService { get; }

        public NavigationService NavigationService { get; }

        public GoalViewModel Goal { get; }

        public User User { get; }

        public static async Task<GoalFixture> CreateAsync()
        {
            var database = await RepositoryTestDatabase.CreateAsync();
            try
            {
                var authenticationService = new AuthenticationService(database.Users);
                var registration = await authenticationService
                    .RegisterAsync("GoalUser01", Password);
                Assert.True(registration.IsSuccess);
                var login = await authenticationService.LoginAsync(
                    "GoalUser01",
                    Password,
                    FixedUtcNow);
                Assert.True(login.IsSuccess);
                Assert.NotNull(login.Value);

                var navigationService = new NavigationService();
                var goal = new GoalViewModel(
                    authenticationService,
                    new GoalService(database.Goals),
                    navigationService,
                    static () => FixedUtcNow);
                navigationService.Navigate(AppRoute.Goal);

                return new GoalFixture(
                    database,
                    authenticationService,
                    navigationService,
                    goal,
                    login.Value!);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public Task LoadAsync()
        {
            return ((INavigationAware)Goal).OnNavigatedToAsync();
        }

        public Task SaveAsync()
        {
            return Goal.SaveGoalCommand.ExecuteAsync(null);
        }

        public ValueTask DisposeAsync()
        {
            return Database.DisposeAsync();
        }
    }
}
