using System.Text.Json;
using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>Sample values for every placeholder, shown in the editor. Uses your real names where set, dummy data for the rest.</summary>
public static class EditorSampleData
{
    public static Dictionary<string, string> Create(MatchState live)
    {
        MatchState copy;
        try
        {
            // Work on a copy, so the live match is never touched
            copy = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(live)) ?? new MatchState();
        }
        catch
        {
            copy = new MatchState();
        }

        copy.Normalize();
        copy.TestMode = true;

        var extra = new Dictionary<string, string>();
        Merge(extra, PlaceholderBuilder.PlayerData(copy, "event.player", 1, 0));
        extra["event.kill.count"] = "3";
        extra["event.kill.label"] = "TRIPLE KILL";
        Merge(extra, PlaceholderBuilder.PlayerData(copy, "mvp.player", 2, 1));
        Merge(extra, PlaceholderBuilder.TeamData(copy, "result", 1));
        extra["timer.end"] = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeMilliseconds().ToString();
        extra["timer.minutes"] = "2";

        return PlaceholderBuilder.Build(copy, true, extra);
    }

    private static void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
    {
        foreach (var (key, value) in source) target[key] = value;
    }
}