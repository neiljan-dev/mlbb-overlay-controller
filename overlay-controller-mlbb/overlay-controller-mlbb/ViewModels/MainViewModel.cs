using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

/// <summary>The main window: the status bar at the top plus whatever screen the Navigator shows.</summary>
public partial class MainViewModel
{
    private readonly DialogService _dialogs;
    private readonly IServiceProvider _provider;

    public Navigator Navigator { get; }
    public ObsService Obs { get; }
    public WebServerHost Server { get; }

    public MainViewModel(Navigator navigator, ObsService obs, WebServerHost server,
                         DialogService dialogs, IServiceProvider provider)
    {
        Navigator = navigator;
        Obs = obs;
        Server = server;
        _dialogs = dialogs;
        _provider = provider;

        Navigator.Navigate<MainMenuViewModel>();   // first screen
    }

    [RelayCommand]
    private void OpenConnection() =>
        _dialogs.ShowConnection(_provider.GetRequiredService<ConnectionViewModel>());
}