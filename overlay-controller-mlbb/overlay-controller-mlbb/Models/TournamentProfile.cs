namespace MlbbOverlay.Models;

public class TournamentProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Team> Teams { get; set; } = new();
}