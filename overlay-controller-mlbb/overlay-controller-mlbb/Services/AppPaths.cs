using System.IO;

namespace MlbbOverlay.Services;

public static class AppPaths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MlbbOverlay");

    public static string Database => Path.Combine(Root, "app.db");
    public static string HeroImages => Path.Combine(Root, "images", "heroes");
    public static string LogoImages => Path.Combine(Root, "images", "logos");
    public static string OverlayAssets => Path.Combine(Root, "images", "overlay-assets");
    public static string MatchStateFile => Path.Combine(Root, "matchstate.json");
    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static string WebRoot => Path.Combine(AppContext.BaseDirectory, "wwwroot");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(HeroImages);
        Directory.CreateDirectory(LogoImages);
        Directory.CreateDirectory(OverlayAssets);
    }
}