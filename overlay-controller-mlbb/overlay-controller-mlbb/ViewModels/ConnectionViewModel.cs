using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;
using System.Runtime.InteropServices.JavaScript;

namespace MlbbOverlay.ViewModels;

/// <summary>The "OBS and Server" window: connection settings and test buttons.</summary>
public partial class ConnectionViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly OverlayDirector _director;
    private readonly DialogService _dialogs;

    public ObsService Obs { get; }
    public WebServerHost Server { get; }

    [ObservableProperty] private string _obsHost;
    [ObservableProperty] private string _obsPort;
    [ObservableProperty] private string _serverPort;
    [ObservableProperty] private bool _autoConnect;
    [ObservableProperty] private string _message = "";

    /// <summary>Filled by the window (a PasswordBox can't be bound directly).</summary>
    public string ObsPassword { get; set; }

    public string OverlayUrl => Server.Port != 0
        ? Server.OverlayUrl
        : $"http://127.0.0.1:{_settings.Current.ServerPort}/overlay";

    public ConnectionViewModel(SettingsService settings, ObsService obs, WebServerHost server,
                               OverlayDirector director, DialogService dialogs)
    {
        _settings = settings;
        _director = director;
        _dialogs = dialogs;
        Obs = obs;
        Server = server;

        var s = settings.Current;
        _obsHost = s.ObsHost;
        _obsPort = s.ObsPort.ToString();
        _serverPort = s.ServerPort.ToString();
        _autoConnect = s.AutoConnectObs;
        ObsPassword = s.ObsPassword;
    }

    private static bool TryPort(string text, out int port) =>
        int.TryParse(text, out port) && port is >= 1 and <= 65535;

    [RelayCommand]
    private void SaveAndConnect()
    {
        if (!TryPort(ObsPort, out var obsPort))
        {
            Message = "The OBS port must be a number from 1 to 65535.";
            return;
        }
        if (!TryPort(ServerPort, out var serverPort))
        {
            Message = "The server port must be a number from 1 to 65535.";
            return;
        }

        var s = _settings.Current;
        var serverPortChanged = s.ServerPort != serverPort;

        s.ObsHost = string.IsNullOrWhiteSpace(ObsHost) ? "127.0.0.1" : ObsHost.Trim();
        s.ObsPort = obsPort;
        s.ObsPassword = ObsPassword ?? "";
        s.AutoConnectObs = AutoConnect;
        s.ServerPort = serverPort;
        _settings.Save();

        Obs.ReconnectNow();

        Message = serverPortChanged
            ? "Saved. Restart the app for the new server port to apply (and update the URL in OBS)."
            : "Saved. Connecting to OBS...";
    }

    [RelayCommand]
    private void CopyUrl()
    {
        _dialogs.CopyToClipboard(OverlayUrl);
        Message = "URL copied.";
    }

    [RelayCommand]
    private async Task ShowTestStays() =>
        await Run(() => _director.ShowAsync("test", OverlayLayers.Base, 0, dummyData: true));

    [RelayCommand]
    private async Task ShowTestThreeSeconds() =>
        await Run(() => _director.ShowAsync("test", OverlayLayers.Event, 3000, dummyData: true));

    [RelayCommand]
    private async Task HideAll() =>
        await Run(() => _director.HideAllAsync());

    private async Task Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { Message = ex.Message; }
    }
}