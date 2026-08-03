using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            var authenticationService = new AuthenticationService(userRepository);
            var navigationService = new NavigationService();

            var loginViewModel = new LoginViewModel(
                authenticationService,
                navigationService,
                static () => DateTimeOffset.UtcNow);
            var registerViewModel = new RegisterViewModel(
                authenticationService,
                navigationService);
            var dashboardViewModel = new AuthenticatedRoutePlaceholderViewModel(
                AppRoute.Dashboard,
                "Dashboard",
                authenticationService,
                navigationService);
            var goalViewModel = new AuthenticatedRoutePlaceholderViewModel(
                AppRoute.Goal,
                "Set Daily Goal",
                authenticationService,
                navigationService);
            var recordActivityViewModel = new AuthenticatedRoutePlaceholderViewModel(
                AppRoute.RecordActivity,
                "Record Activity",
                authenticationService,
                navigationService);

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
