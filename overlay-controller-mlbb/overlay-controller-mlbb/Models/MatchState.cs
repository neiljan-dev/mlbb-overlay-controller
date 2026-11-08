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
}

/// <summary>Everything about the live match. We add more to this in later milestones.</summary>
public class MatchState
{
    public int? ProfileId { get; set; }
    public SeriesFormat Format { get; set; } = SeriesFormat.BO3;
    public TeamSnapshot Team1 { get; set; } = new();   // Blue side in game 1, picks first
    public TeamSnapshot Team2 { get; set; } = new();   // Red side in game 1

    /// <summary>true = Team 1 is Blue. The side swap button (Milestone 7) flips this.</summary>
    public bool BlueIsTeam1 { get; set; } = true;

    public DraftState Draft { get; set; } = new();

    public GameState CurrentState { get; set; } = GameState.DraftPick;

    /// <summary>The scores typed for status.ov (not the series score, which comes in Milestone 7).</summary>
    public int StatusScore1 { get; set; }
    public int StatusScore2 { get; set; }

    // ---- Helpers: teamIndex is 1 or 2 ----
    public TeamSnapshot TeamOf(int teamIndex) => teamIndex == 1 ? Team1 : Team2;
    public List<HeroSnapshot?> PicksOf(int teamIndex) => teamIndex == 1 ? Draft.Picks1 : Draft.Picks2;

    /// <summary>"blue" or "red".</summary>
    public string SideOf(int teamIndex) => (teamIndex == 1) == BlueIsTeam1 ? "blue" : "red";

    /// <summary>The number used in overlay names: 1 = Blue side, 2 = Red side.</summary>
    public int OverlayNumberOf(int teamIndex) => SideOf(teamIndex) == "blue" ? 1 : 2;
}