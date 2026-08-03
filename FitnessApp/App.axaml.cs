using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FitnessApp.Calculators;
using FitnessApp.Common;
using FitnessApp.Data;
using FitnessApp.Repositories;
using FitnessApp.Services;
using FitnessApp.ViewModels;
using FitnessApp.Views;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace FitnessApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var connectionString = InitializeDatabase();
            var userRepository = new UserRepository(connectionString);
            var goalRepository = new GoalRepository(connectionString);
            var activityRepository = new ActivityRepository(connectionString);
            var authenticationService = new AuthenticationService(userRepository);
            var goalService = new GoalService(goalRepository);
            var progressService = new ProgressService(goalRepository, activityRepository);
            var navigationService = new NavigationService();
            IActivityCalculator[] activityCalculators =
            [
                new WalkingCalculator(),
                new SwimmingCalculator(),
                new RunningCalculator(),
                new CyclingCalculator(),
                new StationaryRowingCalculator(),
                new StrengthTrainingCalculator()
            ];
            var activityService = new ActivityService(
                activityRepository,
                activityCalculators);

            var loginViewModel = new LoginViewModel(
                authenticationService,
                navigationService,
                static () => DateTimeOffset.UtcNow);
            var registerViewModel = new RegisterViewModel(
                authenticationService,
                navigationService);
            var dashboardViewModel = new DashboardViewModel(
                authenticationService,
                progressService,
                navigationService,
                static () => DateTimeOffset.UtcNow,
                TimeZoneInfo.Local);
            var goalViewModel = new GoalViewModel(
                authenticationService,
                goalService,
                navigationService,
                static () => DateTimeOffset.UtcNow);
            var recordActivityViewModel = new RecordActivityViewModel(
                authenticationService,
                activityService,
                navigationService,
                static () => DateTimeOffset.UtcNow);

            IReadOnlyDictionary<AppRoute, ViewModelBase> routeViewModels =
                new Dictionary<AppRoute, ViewModelBase>
                {
                    [AppRoute.Login] = loginViewModel,
                    [AppRoute.Register] = registerViewModel,
                    [AppRoute.Dashboard] = dashboardViewModel,
                    [AppRoute.Goal] = goalViewModel,
                    [AppRoute.RecordActivity] = recordActivityViewModel
                };

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    navigationService,
                    routeViewModels),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string InitializeDatabase()
    {
        var databaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FitnessApp");
        Directory.CreateDirectory(databaseDirectory);

        var databasePath = Path.Combine(databaseDirectory, "fitnessapp.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var databaseInitializer = new DatabaseInitializer(connectionString);
        databaseInitializer.InitializeAsync().GetAwaiter().GetResult();

        return connectionString;
    }
}
