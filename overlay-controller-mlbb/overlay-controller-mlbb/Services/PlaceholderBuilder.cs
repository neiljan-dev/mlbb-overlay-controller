using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>
/// Turns the live match into the "placeholder name -> value" list ...
/// </summary>
public static class PlaceholderBuilder
{
    /// <summary>extra = values for one specific overlay (the player of a turtle, the minutes of an ad...).</summary>
    public static Dictionary<string, string> Build(MatchState state, bool dummy,
                                                   Dictionary<string, string>? extra = null)
    {
        dummy |= state.TestMode;

        var data = new Dictionary<string, string>();
        var draft = state.Draft;

        // Overlays are side-based: "blue" and "red". BlueIsTeam1 says which team is on which side.
        var blue = state.BlueIsTeam1 ? state.Team1 : state.Team2;
        var red = state.BlueIsTeam1 ? state.Team2 : state.Team1;
        var bluePicks = state.BlueIsTeam1 ? draft.Picks1 : draft.Picks2;
        var redPicks = state.BlueIsTeam1 ? draft.Picks2 : draft.Picks1;
        var blueBans = state.BlueIsTeam1 ? draft.Bans1 : draft.Bans2;
        var redBans = state.BlueIsTeam1 ? draft.Bans2 : draft.Bans1;

        AddTeam(data, "blue", blue, dummy, "BLUE TEAM", 210);
        AddTeam(data, "red", red, dummy, "RED TEAM", 0);
        AddDraft(data, "blue", bluePicks, blueBans, draft.BansPerTeam, dummy);
        AddDraft(data, "red", redPicks, redBans, draft.BansPerTeam, dummy);

        // Typed status scores and the series
        data["score.blue"] = (state.BlueIsTeam1 ? state.StatusScore1 : state.StatusScore2).ToString();
        data["score.red"] = (state.BlueIsTeam1 ? state.StatusScore2 : state.StatusScore1).ToString();
        data["series.format"] = state.Format.ToString();
        data["series.game"] = state.GameNumber.ToString();
        data["series.towin"] = state.GamesToWin.ToString();
        data["series.score.blue"] = (state.BlueIsTeam1 ? state.SeriesScore1 : state.SeriesScore2).ToString();
        data["series.score.red"] = (state.BlueIsTeam1 ? state.SeriesScore2 : state.SeriesScore1).ToString();

        if (dummy)
        {
            data["score.blue"] = "12";
            data["score.red"] = "8";
            data["series.game"] = "2";
            data["series.score.blue"] = "1";
            data["series.score.red"] = "0";
        }

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
        AddHero(data, prefix, hero, state.TestMode ? playerIndex + 1 : 0);
        foreach (var (key, value) in TeamData(state, $"{prefix}.team", teamIndex)) data[key] = value;
        return data;
    }

    /// <summary>Values for one team: name, logo and side.</summary>
    public static Dictionary<string, string> TeamData(MatchState state, string prefix, int teamIndex)
    {
        var team = state.TeamOf(teamIndex);
        var side = state.SideOf(teamIndex);
        var name = team.Name;
        var logo = LogoUrl(team.LogoFile);

        if (state.TestMode)
        {
            if (string.IsNullOrWhiteSpace(name)) name = side == "blue" ? "BLUE TEAM" : "RED TEAM";
            if (logo == "") logo = DummyImage(side == "blue" ? "B" : "R", side == "blue" ? 210 : 0, 160, 160, 90);
        }

        return new Dictionary<string, string>
        {
            [$"{prefix}.name"] = name,
            [$"{prefix}.logo"] = logo,
            [$"{prefix}.side"] = side
        };
    }

    private static void AddTeam(Dictionary<string, string> data, string side, TeamSnapshot team,
                                bool dummy, string dummyName, int dummyHue)
    {
        data[$"{side}.name"] = Pick(team.Name, dummy, dummyName);

        var logo = LogoUrl(team.LogoFile);
        if (logo == "" && dummy) logo = DummyImage(side == "blue" ? "B" : "R", dummyHue, 160, 160, 90);
        data[$"{side}.logo"] = logo;

        for (var i = 0; i < 5; i++)
        {
            var ign = i < team.Players.Count ? team.Players[i] : "";
            data[$"{side}.player{i + 1}.ign"] = Pick(ign, dummy, $"Player {i + 1}");
        }
    }

    // Every slot always gets values (empty text when nothing is picked),
    // so a cleared slot also clears on screen when the overlay is updated.
    private static void AddDraft(Dictionary<string, string> data, string side,
                                 List<HeroSnapshot?> picks, List<HeroSnapshot?> bans, int banCount, bool dummy)
    {
        for (var i = 0; i < 5; i++)
        {
            var hero = i < picks.Count ? picks[i] : null;
            AddHero(data, $"pick.{side}.slot{i + 1}", hero, dummy ? i + 1 : 0);
        }

        for (var i = 0; i < DraftState.MaxBans; i++)
        {
            var active = dummy || i < banCount;     // ".show" tells overlays which ban slots exist
            var hero = active && i < bans.Count ? bans[i] : null;
            var prefix = $"ban.{side}.slot{i + 1}";

            AddHero(data, prefix, hero, dummy && active ? 10 + i + 1 : 0);
            data[$"{prefix}.show"] = active ? "1" : "0";
        }
    }

    // dummySlot > 0 = fill an empty hero with generated sample art
    private static void AddHero(Dictionary<string, string> data, string prefix, HeroSnapshot? hero, int dummySlot = 0)
    {
        if (hero == null && dummySlot > 0)
        {
            var hue = (dummySlot * 47 + StableNumber(prefix)) % 360;
            data[$"{prefix}.hero"] = $"Hero {dummySlot}";
            data[$"{prefix}.role"] = "Role";
            data[$"{prefix}.icon"] = DummyImage(dummySlot.ToString(), hue, 160, 160, 90);
            data[$"{prefix}.splash"] = DummyImage($"Hero {dummySlot}", hue, 640, 360, 56);
            data[$"{prefix}.full"] = DummyImage($"Hero {dummySlot}", hue, 400, 700, 56);
            return;
        }

        data[$"{prefix}.hero"] = hero?.Name ?? "";
        data[$"{prefix}.role"] = hero?.Role ?? "";
        data[$"{prefix}.icon"] = HeroUrl(hero?.IconFile);
        data[$"{prefix}.splash"] = HeroUrl(hero?.SplashFile);
        data[$"{prefix}.full"] = HeroUrl(hero?.FullBodyFile);
    }

    private static int StableNumber(string text) => text.Aggregate(0, (sum, c) => (sum * 31 + c) % 1000);

    /// <summary>A generated picture (a coloured rectangle with a label) as an inline "data:" image.</summary>
    private static string DummyImage(string label, int hue, int width, int height, int fontSize)
    {
        var svg =
            $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>" +
            "<defs><linearGradient id='g' x1='0' y1='0' x2='1' y2='1'>" +
            $"<stop offset='0' stop-color='hsl({hue},55%,42%)'/><stop offset='1' stop-color='hsl({(hue + 30) % 360},60%,22%)'/>" +
            "</linearGradient></defs>" +
            $"<rect width='{width}' height='{height}' fill='url(#g)'/>" +
            $"<text x='50%' y='50%' dominant-baseline='middle' text-anchor='middle' font-family='Segoe UI,Arial' " +
            $"font-weight='bold' font-size='{fontSize}' fill='white' fill-opacity='0.85'>{label}</text></svg>";

        return "data:image/svg+xml," + Uri.EscapeDataString(svg);
    }

    private static string HeroUrl(string? file) =>
        string.IsNullOrEmpty(file) ? "" : $"/media/heroes/{Uri.EscapeDataString(file)}";

    private static string LogoUrl(string? file) =>
        string.IsNullOrEmpty(file) ? "" : $"/media/logos/{Uri.EscapeDataString(file)}";

    // With dummy = true, empty values are replaced by sample text
    private static string Pick(string value, bool dummy, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? (dummy ? fallback : "") : value;
}