using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpatialTileBuilder.Core.Interfaces;
using SpatialTileBuilder.Infrastructure.Data;
using Serilog;
using SpatialTileBuilder.Infrastructure.Data.Repositories;
using SpatialTileBuilder.Infrastructure.Services;
using SpatialTileBuilder.App.ViewModels;

namespace SpatialTileBuilder.App;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; }

    public App()
    {
        Services = ConfigureServices();
        
        // WPF 전역 예외 처리
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled dispatcher exception occurred: {Message}", e.Exception.Message);
        Log.CloseAndFlush();
        
        MessageBox.Show($"An unexpected error occurred:\n{e.Exception.Message}", 
            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Log.Fatal(exception, "Unhandled domain exception occurred");
        Log.CloseAndFlush();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Serilog Setup
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug()
            .WriteTo.File(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "SpatialTileBuilder", "logs", "log-.txt"), 
                rollingInterval: RollingInterval.Day, 
                retainedFileCountLimit: 7,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddSerilog(dispose: true);
        });

        // Infrastructure
        services.AddSingleton<ILocalDatabase, LocalDatabase>();
        services.AddSingleton<ISessionContext, SessionContext>();
        services.AddSingleton<IPostGISConnectionService, PostGISConnectionService>();
        services.AddSingleton<IConnectionRepository, ConnectionRepository>();
        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<IMapnikStyleService, MapnikStyleService>();
        services.AddTransient<IMapnikRenderer, SpatialTileBuilder.Infrastructure.Mapnik.MockMapnikRenderer>();
        services.AddTransient<ITileGridService, TileGridService>();
        services.AddTransient<ITileGenerationService, TileGenerationService>();
        services.AddSingleton<SpatialTileBuilder.App.Services.ProjectStateService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ConnectionWizardViewModel>();
        services.AddTransient<LayerSelectionViewModel>();
        services.AddTransient<StylePreviewViewModel>();
        services.AddTransient<RegionSelectionViewModel>();
        services.AddTransient<GenerationMonitorViewModel>();

        // Windows/Views
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var logger = Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("App starting...");

            // Initialize DB
            var db = Services.GetRequiredService<ILocalDatabase>();
            await db.InitializeAsync();

            // Show main window
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "App failed to launch.");
            MessageBox.Show($"Failed to start application:\n{ex.Message}", 
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application shutting down...");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
