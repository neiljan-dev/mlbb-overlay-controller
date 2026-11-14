using System.Text.Json;
using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>
/// Decides what is shown in the overlay page and when it disappears:
/// the base layer (one persistent overlay), the event queue, panic, undo and restore.
/// </summary>
public class OverlayDirector
{
    private sealed record BaseEntry(string Key, Dictionary<string, string>? Extra, bool Dummy, bool Undoable);
    private sealed record QueuedEvent(string Key, Dictionary<string, string>? Extra, bool Dummy, int DurationMs);

    private const int GapMs = 450;   // lets the fade-out finish before the next overlay appears

    private readonly WebServerHost _server;
    private readonly MatchStateService _match;
    private readonly SettingsService _settings;
    private readonly object _gate = new();

    // Base layer
    private ShowMessage? _base;                              // what the overlay page shows now
    private BaseEntry? _baseEntry;                           // how to rebuild it
    private readonly List<BaseEntry> _history = new();       // base overlays shown by buttons (for "cancel last")
    private BaseEntry? _restoreEntry;                        // what the panic button hid

    // Event layer
    private readonly List<QueuedEvent> _queue = new();       // waiting
    private ShowMessage? _currentEvent;                      // on screen now
    private CancellationTokenSource? _skipCurrent;           // cuts the current one short
    private bool _pumping;                                   // is the queue loop running?

    public OverlayDirector(WebServerHost server, MatchStateService match, SettingsService settings)
    {
        _server = server;
        _match = match;
        _settings = settings;
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>What an overlay page should show right after it connects.</summary>
    public SnapshotMessage GetSnapshot()
    {
        lock (_gate) return new SnapshotMessage { Base = _base };
    }

    /// <summary>Simple entry point used by the test buttons.</summary>
    public Task ShowAsync(string key, string layer, int durationMs = 0, bool dummyData = false,
                          Dictionary<string, string>? extra = null) =>
        layer == OverlayLayers.Base
            ? ShowBaseAsync(key, extra, false, dummyData)
            : ShowEventAsync(key, extra, dummyData, durationMs > 0 ? durationMs : null);

    // ================= Base layer =================

    /// <summary>Shows a persistent overlay (replaces the previous one). undoable = "Cancel last" can go back to it.</summary>
    public async Task ShowBaseAsync(string key, Dictionary<string, string>? extra = null,
                                    bool undoable = false, bool dummy = false)
    {
        var entry = new BaseEntry(key, extra, dummy, undoable);
        lock (_gate)
        {
            if (undoable) _history.Add(entry);
            else _history.Clear();      // a state overlay starts a new chain
            _restoreEntry = null;
        }
        await ShowBaseCoreAsync(entry);
    }

    private async Task ShowBaseCoreAsync(BaseEntry entry)
    {
        var message = new ShowMessage
        {
            Id = NewId(),
            Key = entry.Key,
            Layer = OverlayLayers.Base,
            DurationMs = 0,
            Data = PlaceholderBuilder.Build(_match.State, entry.Dummy, entry.Extra)
        };

        lock (_gate)
        {
            _base = message;
            _baseEntry = entry;
        }
        await _server.SendAsync("Show", message);
    }

    private async Task HideBaseAsync()
    {
        string? id;
        lock (_gate)
        {
            id = _base?.Id;
            _base = null;
            _baseEntry = null;
        }
        if (id != null) await _server.SendAsync("Hide", id);
    }

    /// <summary>Refreshes the placeholders of what is on screen (after picks, bans, names... change).</summary>
    public async Task UpdateDataAsync()
    {
        Dictionary<string, string> data;
        lock (_gate)
        {
            if (_base == null || _baseEntry == null) return;
            data = PlaceholderBuilder.Build(_match.State, _baseEntry.Dummy, _baseEntry.Extra);
            _base.Data = data;
        }

        try { await _server.SendAsync("Update", data); }
        catch { /* the overlay page will catch up on its next snapshot */ }
    }

    // ================= Event layer (queue) =================

    /// <summary>Queues a short overlay. Whether it waits or replaces depends on the queue mode in Settings.</summary>
    public Task ShowEventAsync(string key, Dictionary<string, string>? extra = null,
                               bool dummy = false, int? durationMs = null)
    {
        var settings = _settings.Current;
        var duration = durationMs ?? Math.Clamp(settings.EventDurationMs, 300, 30000);
        var item = new QueuedEvent(key, extra, dummy, duration);

        CancellationTokenSource? cut = null;
        var startLoop = false;

        lock (_gate)
        {
            if (settings.QueueMode == QueueMode.Replace)
            {
                _queue.Clear();
                cut = _skipCurrent;                 // cut the one on screen short
            }
            else if (_queue.Count >= Math.Max(1, settings.MaxQueueLength))
            {
                return Task.CompletedTask;          // the queue is full: ignore this one
            }

            _queue.Add(item);
            if (!_pumping)
            {
                _pumping = true;
                startLoop = true;
            }
        }

        cut?.Cancel();
        if (startLoop) _ = Task.Run(PumpAsync);
        return Task.CompletedTask;
    }

    // Plays the queued overlays one by one until the queue is empty
    private async Task PumpAsync()
    {
        while (true)
        {
            QueuedEvent item;
            CancellationTokenSource cts;

            lock (_gate)
            {
                if (_queue.Count == 0)
                {
                    _pumping = false;
                    _currentEvent = null;
                    _skipCurrent = null;
                    return;
                }

                item = _queue[0];
                _queue.RemoveAt(0);
                cts = new CancellationTokenSource();
                _skipCurrent = cts;
            }

            var message = new ShowMessage
            {
                Id = NewId(),
                Key = item.Key,
                Layer = OverlayLayers.Event,
                DurationMs = item.DurationMs,
                Data = PlaceholderBuilder.Build(_match.State, item.Dummy, item.Extra)
            };
            lock (_gate) _currentEvent = message;

            try
            {
                await _server.SendAsync("Show", message);
                await Task.Delay(item.DurationMs, cts.Token);
            }
            catch (OperationCanceledException) { /* cut short: that's fine */ }
            catch { /* sending failed: carry on with the queue */ }

            try { await _server.SendAsync("Hide", message.Id); }
            catch { }

            bool more;
            lock (_gate)
            {
                _currentEvent = null;
                more = _queue.Count > 0;
            }

            // One after another: a short pause so the fade-out finishes. Replace mode goes straight on.
            if (more && _settings.Current.QueueMode == QueueMode.Sequential)
            {
                try { await Task.Delay(Math.Max(GapMs, OutDurationMs(item.Key) + 50)); }
                catch { }
            }
        }
    }

    // ================= Panic, undo, restore =================

    /// <summary>The panic button: hides everything. "Restore" can bring the base overlay back.</summary>
    public async Task HideAllAsync()
    {
        CancellationTokenSource? cut;
        lock (_gate)
        {
            _queue.Clear();
            cut = _skipCurrent;
            if (_baseEntry != null) _restoreEntry = _baseEntry;
            _base = null;
            _baseEntry = null;
        }

        cut?.Cancel();
        await _server.SendAsync("HideAll");
    }

    /// <summary>Clears everything without remembering it (used when the game state changes).</summary>
    public async Task ClearAllAsync()
    {
        CancellationTokenSource? cut;
        lock (_gate)
        {
            _queue.Clear();
            cut = _skipCurrent;
            _base = null;
            _baseEntry = null;
            _history.Clear();
            _restoreEntry = null;
        }

        cut?.Cancel();
        await _server.SendAsync("HideAll");
    }

    /// <summary>Brings back the overlay that the panic button hid.</summary>
    public async Task RestoreAsync()
    {
        BaseEntry? entry;
        lock (_gate)
        {
            entry = _restoreEntry;
            _restoreEntry = null;
        }
        if (entry != null) await ShowBaseCoreAsync(entry);
    }

    /// <summary>
    /// Undo: removes the newest waiting overlay; otherwise cancels the one on screen;
    /// otherwise goes back to the previous base overlay (or hides it).
    /// </summary>
    public async Task CancelLastAsync()
    {
        CancellationTokenSource? cut = null;
        BaseEntry? showAgain = null;
        var hideBase = false;

        lock (_gate)
        {
            if (_queue.Count > 0)
            {
                _queue.RemoveAt(_queue.Count - 1);
            }
            else if (_currentEvent != null)
            {
                cut = _skipCurrent;
            }
            else if (_base != null && _history.Count > 0)
            {
                _history.RemoveAt(_history.Count - 1);
                if (_history.Count > 0) showAgain = _history[^1];
                else hideBase = true;
            }
        }

        cut?.Cancel();
        if (showAgain != null) await ShowBaseCoreAsync(showAgain);
        else if (hideBase) await HideBaseAsync();

    }


            /// <summary>How long the exit animations of a designed overlay take (0 if it has none). Used for the queue pause.</summary>
    private int OutDurationMs(string key)
    {
        try
        {
            var json = _server.DesignProvider?.Invoke(key);
            if (json == null) return 0;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("elements", out var elements)) return 0;

            var longest = 0.0;
            foreach (var element in elements.EnumerateArray())
            {
                if (!element.TryGetProperty("anim", out var anim) || anim.ValueKind != JsonValueKind.Object) continue;
                if (!anim.TryGetProperty("out", out var exit) || exit.ValueKind != JsonValueKind.Object) continue;

                var ms = exit.TryGetProperty("ms", out var m) && m.TryGetDouble(out var mv) ? mv : 400;
                var delay = exit.TryGetProperty("delay", out var d) && d.TryGetDouble(out var dv) ? dv : 0;
                longest = Math.Max(longest, ms + delay);
            }
            return (int)longest;
        }
        catch
        {
            return 0;
        }
    }
}