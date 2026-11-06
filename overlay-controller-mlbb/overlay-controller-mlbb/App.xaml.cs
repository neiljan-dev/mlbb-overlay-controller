using Microsoft.Extensions.DependencyInjection;
using MlbbOverlay.Services;
using MlbbOverlay.ViewModels;
using overlay_controller_mlbb;
using System.Windows;

namespace MlbbOverlay;

public partial class App : Application
{
    public static IServiceProvider Provider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureCreated();

        var services = new ServiceCollection();
        services.AddSingleton<Navigator>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainMenuViewModel>();
        services.AddSingleton<MainWindow>();
        Provider = services.BuildServiceProvider();

        var window = Provider.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }
}