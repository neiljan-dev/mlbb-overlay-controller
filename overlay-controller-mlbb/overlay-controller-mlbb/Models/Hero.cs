namespace MlbbOverlay.Models;
public class Hero
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public HeroRole Role { get; set; } = HeroRole.Unassigned;

    public string? IconFile { get; set; }
    public string? SplashFile { get; set; }
    public string? FullBodyFile { get; set; }

    public string SplashStatus => string.IsNullOrEmpty(SplashFile) ? "-" : "OK";
    public string FullBodyStatus => string.IsNullOrEmpty(FullBodyFile) ? "-" : "OK";
}