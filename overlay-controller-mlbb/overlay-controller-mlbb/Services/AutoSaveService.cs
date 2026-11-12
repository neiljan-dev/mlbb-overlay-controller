using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>Saves the live match to matchstate.json every 2 seconds (if it changed) and when the app closes.</summary>
public class AutoSaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly MatchStateService _match;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private string _lastSaved = "";

    public AutoSaveService(MatchStateService match)
    {
        _match = match;
        _timer.Tick += (_, _) => SaveNow();
    }

    /// <summary>Reads the saved match. Returns null if there is none or it can't be read.</summary>
    public MatchState? Load()
    {
        try
        {
            if (!File.Exists(AppPaths.MatchStateFile)) return null;

            var json = File.ReadAllText(AppPaths.MatchStateFile);
            var state = JsonSerializer.Deserialize<MatchState>(json);
            state?.Normalize();
            return state;
        }
        catch
        {
            return null;   // damaged file: start fresh
        }
    }

    public void Start() => _timer.Start();

    public void SaveNow()
    {
        try
        {
            var json = JsonSerializer.Serialize(_match.State, JsonOptions);
            if (json == _lastSaved) return;

            // Write to a temporary file first, so a crash in the middle can't damage the real one
            var temp = AppPaths.MatchStateFile + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, AppPaths.MatchStateFile, overwrite: true);
            _lastSaved = json;
        }
        catch
        {
            // try again on the next tick
        }
    }
}