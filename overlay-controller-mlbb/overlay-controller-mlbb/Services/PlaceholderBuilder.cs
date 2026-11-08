using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>Turns the live match into the "placeholder name -> value" list that overlays use.</summary>
public static class PlaceholderBuilder
{
    /// <summary>extra = values for one specific overlay (the player of a turtle, the minutes of an ad...).</summary>
    public static Dictionary<string, string> Build(MatchState state, bool dummy,
                                                   Dictionary<string, string>? extra = null)
    {
        var data = new Dictionary<string, string>();
        var draft = state.Draft;

        // Overlays are side-based: "blue" and "red". BlueIsTeam1 says which team is on which side.
        var blue = state.BlueIsTeam1 ? state.Team1 : state.Team2;
        var red = state.BlueIsTeam1 ? state.Team2 : state.Team1;
        var bluePicks = state.BlueIsTeam1 ? draft.Picks1 : draft.Picks2;
        var redPicks = state.BlueIsTeam1 ? draft.Picks2 : draft.Picks1;
        var blueBans = state.BlueIsTeam1 ? draft.Bans1 : draft.Bans2;
        var redBans = state.BlueIsTeam1 ? draft.Bans2 : draft.Bans1;

        AddTeam(data, "blue", blue, dummy, "BLUE TEAM");
        AddTeam(data, "red", red, dummy, "RED TEAM");
        AddDraft(data, "blue", bluePicks, blueBans, draft.BansPerTeam);
        AddDraft(data, "red", redPicks, redBans, draft.BansPerTeam);

        data["score.blue"] = (state.BlueIsTeam1 ? state.StatusScore1 : state.StatusScore2).ToString();
        data["score.red"] = (state.BlueIsTeam1 ? state.StatusScore2 : state.StatusScore1).ToString();
        data["series.format"] = state.Format.ToString();

        if (extra != null)
        {
            foreach (var (key, value) in extra) data[key] = value;
        }
        return data;
    }

    /// <summary>Values for one player: IGN, their hero and their team. prefix = "event.player" or "mvp.player".</summary>
    public static Dictionary<string, string> PlayerData(MatchState state, string prefix, int teamIndex, int playerIndex)
    {
        var team = state.TeamOf(teamIndex);
        var ign = playerIndex < team.Players.Count ? team.Players[playerIndex] : "";
        if (string.IsNullOrWhiteSpace(ign)) ign = $"Player {playerIndex + 1}";

        var picks = state.PicksOf(teamIndex);
        var hero = playerIndex < picks.Count ? picks[playerIndex] : null;

        var data = new Dictionary<string, string> { [$"{prefix}.ign"] = ign };
        AddHero(data, prefix, hero);
        foreach (var (key, value) in TeamData(state, $"{prefix}.team", teamIndex)) data[key] = value;
        return data;
    }

    /// <summary>Values for one team: name, logo and side.</summary>
    public static Dictionary<string, string> TeamData(MatchState state, string prefix, int teamIndex)
    {
        var team = state.TeamOf(teamIndex);
        return new Dictionary<string, string>
        {
            [$"{prefix}.name"] = team.Name,
            [$"{prefix}.logo"] = LogoUrl(team.LogoFile),
            [$"{prefix}.side"] = state.SideOf(teamIndex)
        };
    }

    private static void AddTeam(Dictionary<string, string> data, string side, TeamSnapshot team,
                                bool dummy, string dummyName)
    {
        data[$"{side}.name"] = Pick(team.Name, dummy, dummyName);
        data[$"{side}.logo"] = LogoUrl(team.LogoFile);

        for (var i = 0; i < 5; i++)
        {
            var ign = i < team.Players.Count ? team.Players[i] : "";
            data[$"{side}.player{i + 1}.ign"] = Pick(ign, dummy, $"Player {i + 1}");
        }
    }

    // Every slot always gets values (empty text when nothing is picked),
    // so a cleared slot also clears on screen when the overlay is updated.
    private static void AddDraft(Dictionary<string, string> data, string side,
                                 List<HeroSnapshot?> picks, List<HeroSnapshot?> bans, int banCount)
    {
        for (var i = 0; i < 5; i++)
            AddHero(data, $"pick.{side}.slot{i + 1}", i < picks.Count ? picks[i] : null);

        for (var i = 0; i < DraftState.MaxBans; i++)
        {
            var hero = i < banCount && i < bans.Count ? bans[i] : null;
            AddHero(data, $"ban.{side}.slot{i + 1}", hero);
        }
    }

    private static void AddHero(Dictionary<string, string> data, string prefix, HeroSnapshot? hero)
    {
        data[$"{prefix}.hero"] = hero?.Name ?? "";
        data[$"{prefix}.role"] = hero?.Role ?? "";
        data[$"{prefix}.icon"] = HeroUrl(hero?.IconFile);
        data[$"{prefix}.splash"] = HeroUrl(hero?.SplashFile);
        data[$"{prefix}.full"] = HeroUrl(hero?.FullBodyFile);
    }

    private static string HeroUrl(string? file) =>
        string.IsNullOrEmpty(file) ? "" : $"/media/heroes/{Uri.EscapeDataString(file)}";

    private static string LogoUrl(string? file) =>
        string.IsNullOrEmpty(file) ? "" : $"/media/logos/{Uri.EscapeDataString(file)}";

    // With dummy = true, empty values are replaced by sample text (used for tests)
    private static string Pick(string value, bool dummy, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? (dummy ? fallback : "") : value;
}