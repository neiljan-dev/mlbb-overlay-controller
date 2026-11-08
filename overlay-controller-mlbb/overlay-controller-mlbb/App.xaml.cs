using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MlbbOverlay.Data;
using MlbbOverlay.Services;
using MlbbOverlay.ViewModels;

namespace MlbbOverlay;

public partial class App : Application
{
    public static IServiceProvider Provider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureCreated();

        // Create or update the database tables
        try
        {
            using var db = new AppDbContext();
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not open the database:\n\n" + ex.Message,
                            "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Register everything the app needs. New services go ABOVE BuildServiceProvider().
        var services = new ServiceCollection();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<Navigator>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ImageStore>();
        services.AddSingleton<HeroService>();
        services.AddSingleton<BulkImportService>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<MatchStateService>();
        services.AddSingleton<WebServerHost>();
        services.AddSingleton<ObsService>();
        services.AddSingleton<OverlayDirector>();
        services.AddSingleton<GameActionService>();
        services.AddSingleton<HotkeyService>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainMenuViewModel>();
        services.AddTransient<DatabaseViewModel>();
        services.AddTransient<TeamSetupViewModel>();
        services.AddTransient<ConnectionViewModel>();
        services.AddTransient<ControllerViewModel>();
        services.AddTransient<HeroSelectionViewModel>();
        services.AddTransient<GameStateViewModel>();
        services.AddTransient<ControllerSettingsViewModel>();

        services.AddSingleton<MainWindow>();
        Provider = services.BuildServiceProvider();

        // Start the overlay server and the OBS connection
        var settings = Provider.GetRequiredService<SettingsService>();
        var director = Provider.GetRequiredService<OverlayDirector>();
        _ = Provider.GetRequiredService<WebServerHost>()
                    .StartAsync(settings.Current.ServerPort, () => director.GetSnapshot());
        Provider.GetRequiredService<ObsService>().Start();

        var window = Provider.GetRequiredService<MainWindow>();
        MainWindow = window;

        // Hotkeys listen to this window's keyboard
        Provider.GetRequiredService<HotkeyService>().Attach(window);

        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Provider != null)
        {
            try { Task.Run(() => Provider.GetRequiredService<WebServerHost>().StopAsync()).Wait(TimeSpan.FromSeconds(3)); }
            catch { }
            try { Provider.GetRequiredService<ObsService>().Stop(); }
            catch { }
        }
        base.OnExit(e);
    }
}