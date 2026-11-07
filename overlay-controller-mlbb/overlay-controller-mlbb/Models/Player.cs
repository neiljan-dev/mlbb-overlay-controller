namespace MlbbOverlay.Models;

public class Player
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int Order { get; set; }             
    public string Ign { get; set; } = "";    
}