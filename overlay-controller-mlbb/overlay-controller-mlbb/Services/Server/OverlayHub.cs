using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.SignalR;
using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>Counts how many overlay pages are connected (OBS browser source, preview, a browser tab...).</summary>
public partial class ClientTracker : ObservableObject
{
    private int _n;

    [ObservableProperty]
    private int _count;

    public void Add() => Count = Interlocked.Increment(ref _n);
    public void Remove() => Count = Interlocked.Decrement(ref _n);
}

/// <summary>Lets the hub ask the app "what is on screen right now?".</summary>
public class SnapshotSource
{
    private readonly Func<SnapshotMessage> _get;

    public SnapshotSource(Func<SnapshotMessage> get)
    {
        _get = get;
    }

    public SnapshotMessage Get() => _get();
}

/// <summary>The live connection between the app and the overlay page.</summary>
public class OverlayHub : Hub
{
    private readonly SnapshotSource _snapshots;
    private readonly ClientTracker _clients;

    public OverlayHub(SnapshotSource snapshots, ClientTracker clients)
    {
        _snapshots = snapshots;
        _clients = clients;
    }

    public override Task OnConnectedAsync()
    {
        _clients.Add();
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _clients.Remove();
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Called by the overlay page after it connects or reconnects.</summary>
    public SnapshotMessage RequestSnapshot() => _snapshots.Get();
}