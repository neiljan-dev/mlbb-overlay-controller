using System.Text.Json.Serialization;

namespace MlbbOverlay.Models;

public enum SeriesFormat { BO1, BO3, BO5 }

public enum GameState { DraftPick, Ingame, Postgame, Neiljan }

/// <summary>A copy of a team for the current match (changing the database later doesn't affect it).</summary>
public class TeamSnapshot
{
    public string Name { get; set; } = "";
    public string? LogoFile { get; set; }
    public List<string> Players { get; set; } = new() { "", "", "", "", "" };
}

/// <summary>A copy of a hero as picked or banned in this match.</summary>
public class HeroSnapshot
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string? IconFile { get; set; }
    public string? SplashFile { get; set; }
    public string? FullBodyFile { get; set; }

    public static HeroSnapshot From(Hero hero) => new()
    {
        Id = hero.Id,
        Name = hero.Name,
        Role = hero.Role.ToString(),
        IconFile = hero.IconFile,
        SplashFile = hero.SplashFile,
        FullBodyFile = hero.FullBodyFile
    };
}

/// <summary>Picks and bans of the current game. Index 0 is slot 1. "1" and "2" are Team 1 and Team 2.</summary>
public class DraftState
{
    public const int MaxBans = 6;

    public int BansPerTeam { get; set; } = 3;

    public List<HeroSnapshot?> Picks1 { get; set; } = Empty(5);
    public List<HeroSnapshot?> Picks2 { get; set; } = Empty(5);
    public List<HeroSnapshot?> Bans1 { get; set; } = Empty(MaxBans);
    public List<HeroSnapshot?> Bans2 { get; set; } = Empty(MaxBans);

    private static List<HeroSnapshot?> Empty(int count) =>
        Enumerable.Repeat<HeroSnapshot?>(null, count).ToList();

    private static List<HeroSnapshot?> Pad(List<HeroSnapshot?>? list, int count)
    {
        list ??= new List<HeroSnapshot?>();
        while (list.Count < count) list.Add(null);
        return list;
    }

    /// <summary>Makes a loaded draft safe to use (right list sizes).</summary>
    public void Normalize()
    {
        Picks1 = Pad(Picks1, 5);
        Picks2 = Pad(Picks2, 5);
        Bans1 = Pad(Bans1, MaxBans);
        Bans2 = Pad(Bans2, MaxBans);
        BansPerTeam = Math.Clamp(BansPerTeam, 0, MaxBans);
    }

    /// <summary>Empties every pick and ban but keeps the lists themselves (the screens point at them).</summary>
    public void ClearHeroes()
    {
        foreach (var list in new[] { Picks1, Picks2, Bans1, Bans2 })
            for (var i = 0; i < list.Count; i++) list[i] = null;
    }
}

/// <summary>Everything about the live match. It is saved to matchstate.json by the auto-save.</summary>
public class MatchState
{
    public int? ProfileId { get; set; }
    public SeriesFormat Format { get; set; } = SeriesFormat.BO3;
    public TeamSnapshot Team1 { get; set; } = new();   // Blue side in game 1, picks first
    public TeamSnapshot Team2 { get; set; } = new();   // Red side in game 1

    /// <summary>true = Team 1 is Blue. "Swap sides" flips this.</summary>
    public bool BlueIsTeam1 { get; set; } = true;

    public DraftState Draft { get; set; } = new();

    public GameState CurrentState { get; set; } = GameState.DraftPick;

    /// <summary>The scores typed for status.ov.</summary>
    public int StatusScore1 { get; set; }
    public int StatusScore2 { get; set; }

    // ---- Series ----
    public int GameNumber { get; set; } = 1;
    public int SeriesScore1 { get; set; }
    public int SeriesScore2 { get; set; }

    /// <summary>The team (1 or 2) that already got the series point of this game. Prevents counting twice.</summary>
    public int? CurrentGameWinner { get; set; }

    /// <summary>Add a series point automatically when Victory is pressed.</summary>
    public bool AutoSeriesPoint { get; set; } = true;

    /// <summary>Test mode: overlays are filled with dummy data. Never saved, so it is off at every start.</summary>
    [JsonIgnore]
    public bool TestMode { get; set; }

    /// <summary>How many games a team must win: BO1 = 1, BO3 = 2, BO5 = 3.</summary>
    [JsonIgnore]
    public int GamesToWin => Format switch
    {
        SeriesFormat.BO1 => 1,
        SeriesFormat.BO3 => 2,
        _ => 3
    };

    // ---- Helpers: teamIndex is 1 or 2 ----
    public TeamSnapshot TeamOf(int teamIndex) => teamIndex == 1 ? Team1 : Team2;
    public List<HeroSnapshot?> PicksOf(int teamIndex) => teamIndex == 1 ? Draft.Picks1 : Draft.Picks2;

    /// <summary>"blue" or "red".</summary>
    public string SideOf(int teamIndex) => (teamIndex == 1) == BlueIsTeam1 ? "blue" : "red";

    /// <summary>The number used in overlay names: 1 = Blue side, 2 = Red side.</summary>
    public int OverlayNumberOf(int teamIndex) => SideOf(teamIndex) == "blue" ? 1 : 2;

    public bool HasTeams() =>
        !string.IsNullOrWhiteSpace(Team1.Name) || !string.IsNullOrWhiteSpace(Team2.Name);

    public string Summary() =>
        $"{(string.IsNullOrWhiteSpace(Team1.Name) ? "Team 1" : Team1.Name)}  vs  " +
        $"{(string.IsNullOrWhiteSpace(Team2.Name) ? "Team 2" : Team2.Name)}\n" +
        $"Game {GameNumber}, series {SeriesScore1} - {SeriesScore2} ({Format}), state: {CurrentState}";

    /// <summary>Makes a loaded match safe to use (nothing missing, right list sizes).</summary>
    public void Normalize()
    {
        Team1 ??= new TeamSnapshot();
        Team2 ??= new TeamSnapshot();
        Draft ??= new DraftState();

        foreach (var team in new[] { Team1, Team2 })
        {
            team.Name ??= "";
            team.Players ??= new List<string>();
            while (team.Players.Count < 5) team.Players.Add("");
        }

        Draft.Normalize();
        GameNumber = Math.Max(1, GameNumber);
    }
}