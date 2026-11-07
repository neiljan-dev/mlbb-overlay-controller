namespace MlbbOverlay.Models;

public enum SeriesFormat { BO1, BO3, BO5, BO7 }

public class TeamSnapshot
{
    public string Name { get; set; } = "";
    public string? LogoFile { get; set; }
    public List<string> Players { get; set; } = new() { "", "", "", "", "" };
}

public class MatchState
{
    public int? ProfileId { get; set; }
    public SeriesFormat Format { get; set; } = SeriesFormat.BO3;
    public TeamSnapshot Team1 { get; set; } = new();   // Blue side in game 1, picks first
    public TeamSnapshot Team2 { get; set; } = new();   // Red side in game 1
}