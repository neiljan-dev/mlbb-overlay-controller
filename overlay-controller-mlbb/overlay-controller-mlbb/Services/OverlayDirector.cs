using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>
/// Decides what is shown in the overlay page and when it disappears.
/// Milestone 6 adds the queue, undo and panic button here.
/// </summary>
public class OverlayDirector
{
    private readonly WebServerHost _server;
    private readonly MatchStateService _match;
    private readonly object _gate = new();
    private ShowMessage? _base;   // the persistent overlay currently on screen

    public OverlayDirector(WebServerHost server, MatchStateService match)
    {
        _server = server;
        _match = match;
    }

    /// <summary>What an overlay page should show right after it connects.</summary>
    public SnapshotMessage GetSnapshot()
    {
        lock (_gate) return new SnapshotMessage { Base = _base };
    }

    public async Task ShowAsync(string key, string layer, int durationMs = 0, bool dummyData = false)
    {
        var message = new ShowMessage
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Key = key,
            Layer = layer,
            DurationMs = durationMs,
            Data = PlaceholderBuilder.Build(_match.State, dummyData)
        };

        if (layer == OverlayLayers.Base)
        {
            lock (_gate) _base = message;
        }

        await _server.SendAsync("Show", message);

        if (durationMs > 0) _ = HideAfterAsync(message.Id, durationMs);
    }

    private async Task HideAfterAsync(string id, int delayMs)
    {
        try
        {
            await Task.Delay(delayMs);
            await HideAsync(id);
        }
        catch
        {
            // nothing to do
        }
    }

    public async Task HideAsync(string id)
    {
        lock (_gate)
        {
            if (_base?.Id == id) _base = null;
        }
        await _server.SendAsync("Hide", id);
    }

    public async Task HideAllAsync()
    {
        lock (_gate) _base = null;
        await _server.SendAsync("HideAll");
    }
}