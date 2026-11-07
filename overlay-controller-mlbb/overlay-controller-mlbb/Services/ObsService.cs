using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using OBSWebsocketDotNet;

namespace MlbbOverlay.Services;

public enum ObsStatus { Disconnected, Connecting, Connected }

/// <summary>Connects to OBS (WebSocket v5), keeps retrying, and reports the status for the green/red dot.</summary>
public partial class ObsService : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly object _gate = new();
    private OBSWebsocket? _obs;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private ObsStatus _status = ObsStatus.Disconnected;
    [ObservableProperty] private string _statusText = "OBS: not connected";

    public ObsService(SettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>Starts the background loop that (re)connects every 4 seconds while OBS is not connected.</summary>
    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if (_settings.Current.AutoConnectObs && Status != ObsStatus.Connected)
                    TryConnect(showConnecting: false);

                try { await Task.Delay(TimeSpan.FromSeconds(4), token); }
                catch (OperationCanceledException) { break; }
            }
        });
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        lock (_gate) Drop();
    }

    /// <summary>Connects right now using the saved settings (used by "Save and connect").</summary>
    public void ReconnectNow() => Task.Run(() => TryConnect(showConnecting: true));

    private void TryConnect(bool showConnecting)
    {
        var s = _settings.Current;

        lock (_gate)
        {
            Drop();

            var obs = new OBSWebsocket();
            _obs = obs;

            // Ignore events from an old connection attempt (ReferenceEquals check)
            obs.Connected += (_, _) =>
            {
                if (ReferenceEquals(obs, _obs)) SetStatus(ObsStatus.Connected, "OBS: connected");
            };
            obs.Disconnected += (_, _) =>
            {
                if (ReferenceEquals(obs, _obs))
                    SetStatus(ObsStatus.Disconnected, "OBS: not connected (retrying every 4 seconds)");
            };

            if (showConnecting) SetStatus(ObsStatus.Connecting, "OBS: connecting...");

            try
            {
                obs.ConnectAsync($"ws://{s.ObsHost}:{s.ObsPort}", s.ObsPassword);
            }
            catch (Exception ex)
            {
                SetStatus(ObsStatus.Disconnected, "OBS: " + ex.Message.Split('\n')[0]);
            }
        }
    }

    // Closes the current connection (caller holds the lock)
    private void Drop()
    {
        var old = _obs;
        _obs = null;
        if (old == null) return;
        try { old.Disconnect(); } catch { /* was never connected */ }
    }

    // Events arrive on background threads, so change the properties on the UI thread
    private void SetStatus(ObsStatus status, string text)
    {
        void Apply()
        {
            Status = status;
            StatusText = text;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) Apply();
        else dispatcher.BeginInvoke(Apply);
    }
}