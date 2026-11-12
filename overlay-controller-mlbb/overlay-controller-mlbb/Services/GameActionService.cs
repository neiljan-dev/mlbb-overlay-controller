using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>
/// What every Game State button does: which overlay to show and with which data.
/// teamIndex is 1 or 2 (Team 1 or Team 2), playerIndex is 0 to 4.
/// </summary>
public class GameActionService
{
    private readonly MatchStateService _match;
    private readonly OverlayDirector _director;

    public GameActionService(MatchStateService match, OverlayDirector director)
    {
        _match = match;
        _director = director;
    }

    private MatchState State => _match.State;

    // ---------- State switch ----------

    private static string? BaseKeyFor(GameState state) => state switch
    {
        GameState.DraftPick => "draftpick.ov",
        GameState.Ingame => "ingame.ov",
        _ => null            // Postgame and Neiljan show something only after you press a button
    };

    public async Task SetStateAsync(GameState state)
    {
        State.CurrentState = state;
        await _director.ClearAllAsync();

        var key = BaseKeyFor(state);
        if (key != null) await _director.ShowBaseAsync(key);
    }

    /// <summary>Called when the Controller opens: make sure the overlay of the current state is on screen.</summary>
    public async Task EnsureStateOverlayAsync()
    {
        var key = BaseKeyFor(State.CurrentState);
        if (key != null && _director.GetSnapshot().Base?.Key != key)
            await _director.ShowBaseAsync(key);
    }

    // ---------- Panic, undo, restore ----------

    public Task PanicAsync() => _director.HideAllAsync();

    public Task CancelLastAsync() => _director.CancelLastAsync();

    public Task RestoreAsync() => _director.RestoreAsync();

    // ---------- Ingame (event layer, goes through the queue) ----------

    public Task TurtleSlainAsync(int teamIndex, int playerIndex) => PlayerEventAsync("turtle", teamIndex, playerIndex);

    public Task LordSlainAsync(int teamIndex, int playerIndex) => PlayerEventAsync("lord", teamIndex, playerIndex);

    private Task PlayerEventAsync(string kind, int teamIndex, int playerIndex)
    {
        var key = $"{State.OverlayNumberOf(teamIndex)}.{kind}";     // 1.turtle (Blue) or 2.turtle (Red)
        var extra = PlaceholderBuilder.PlayerData(State, "event.player", teamIndex, playerIndex);
        return _director.ShowEventAsync(key, extra);
    }

    public Task TurtleSpawnAsync() => _director.ShowEventAsync("turtle.spawn");

    public Task LordSpawnAsync() => _director.ShowEventAsync("lord.spawn");

    public Task KillAsync(int teamIndex, int playerIndex, int count)
    {
        var extra = PlaceholderBuilder.PlayerData(State, "event.player", teamIndex, playerIndex);
        extra["event.kill.count"] = count.ToString();
        extra["event.kill.label"] = count switch
        {
            2 => "DOUBLE KILL",
            3 => "TRIPLE KILL",
            4 => "MANIAC",
            _ => "SAVAGE"
        };
        return _director.ShowEventAsync($"kill.{count}", extra);
    }

    public Task VictoryAsync(int teamIndex) =>
        _director.ShowEventAsync($"{State.OverlayNumberOf(teamIndex)}.win");

    // ---------- Postgame (base layer, stays until you switch) ----------

    public Task ResultAsync(int teamIndex)
    {
        var extra = PlaceholderBuilder.TeamData(State, "result", teamIndex);
        return _director.ShowBaseAsync("result.ov", extra, undoable: true);
    }

    public Task MvpAsync(int teamIndex, int playerIndex)
    {
        var extra = PlaceholderBuilder.PlayerData(State, "mvp.player", teamIndex, playerIndex);
        return _director.ShowBaseAsync("mvp.ov", extra, undoable: true);
    }

    public Task StatusAsync(int scoreTeam1, int scoreTeam2)
    {
        State.StatusScore1 = scoreTeam1;
        State.StatusScore2 = scoreTeam2;
        return _director.ShowBaseAsync("status.ov", undoable: true);
    }

    // ---------- Neiljan / ads (base layer, stays until you switch) ----------

    /// <summary>ad1 or ad2 with a countdown. The overlay page counts down to an end time.</summary>
    public Task AdAsync(int adNumber, int minutes)
    {
        var end = DateTimeOffset.UtcNow.AddMinutes(minutes).ToUnixTimeMilliseconds();
        var extra = new Dictionary<string, string>
        {
            ["timer.end"] = end.ToString(),
            ["timer.minutes"] = minutes.ToString()
        };
        return _director.ShowBaseAsync($"ad{adNumber}", extra, undoable: true);
    }

    /// <summary>ad3: looping ads, no timer.</summary>
    public Task Ad3Async() => _director.ShowBaseAsync("ad3", undoable: true);

    // ---------- Overlay Animation panel: play any overlay with a sample player ----------

    /// <summary>
    /// Plays or replays an overlay from the Overlay Animation list. It never changes the series score
    /// or the game state. Use test mode to fill it with dummy data.
    /// </summary>
    public Task PlayOverlayAsync(OverlayInfo info)
    {
        var blue = State.BlueIsTeam1 ? 1 : 2;
        var red = 3 - blue;

        if (info.Key.StartsWith("kill."))
        {
            var count = int.Parse(info.Key[5..]);
            return KillAsync(count % 2 == 0 ? blue : red, 0, count);   // alternate sides to check the colors
        }

        return info.Key switch
        {
            "1.turtle" => TurtleSlainAsync(blue, 0),
            "2.turtle" => TurtleSlainAsync(red, 0),
            "1.lord" => LordSlainAsync(blue, 0),
            "2.lord" => LordSlainAsync(red, 0),
            "turtle.spawn" => TurtleSpawnAsync(),
            "lord.spawn" => LordSpawnAsync(),
            "1.win" or "2.win" => _director.ShowEventAsync(info.Key),
            "result.ov" => ResultAsync(blue),
            "mvp.ov" => MvpAsync(red, 0),
            "ad1" => AdAsync(1, 2),
            "ad2" => AdAsync(2, 2),
            "ad3" => Ad3Async(),
            _ => info.Layer == OverlayLayers.Base
                ? _director.ShowBaseAsync(info.Key, undoable: true)
                : _director.ShowEventAsync(info.Key)
        };
    }

}