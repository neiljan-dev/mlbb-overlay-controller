using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>Turns the live match into the "placeholder name -> value" list that overlays use.</summary>
public static class PlaceholderBuilder
{
    public static Dictionary<string, string> Build(MatchState state, bool dummy)
    {
        var data = new Dictionary<string, string>();

        // Until the side swap exists (Milestone 7), Team 1 is always Blue.
        AddTeam(data, "blue", state.Team1, dummy, "BLUE TEAM");
        AddTeam(data, "red", state.Team2, dummy, "RED TEAM");
        data["series.format"] = state.Format.ToString();
        return data;
    }

    private static void AddTeam(Dictionary<string, string> data, string side, TeamSnapshot team,
                                bool dummy, string dummyName)
    {
        data[$"{side}.name"] = Pick(team.Name, dummy, dummyName);
        data[$"{side}.logo"] = string.IsNullOrEmpty(team.LogoFile)
            ? ""
            : $"/media/logos/{Uri.EscapeDataString(team.LogoFile)}";

        for (var i = 0; i < 5; i++)
        {
            var ign = i < team.Players.Count ? team.Players[i] : "";
            data[$"{side}.player{i + 1}.ign"] = Pick(ign, dummy, $"Player {i + 1}");
        }
    }

    // With dummy = true, empty values are replaced by sample text (used for tests)
    private static string Pick(string value, bool dummy, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? (dummy ? fallback : "") : value;
}