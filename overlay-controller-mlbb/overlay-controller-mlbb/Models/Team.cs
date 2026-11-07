using System.Numerics;

namespace MlbbOverlay.Models;

public class Team
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public string Name { get; set; } = "";
    public string? LogoFile { get; set; }     
    public List<Player> Players { get; set; } = new();
}