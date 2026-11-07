using System.IO;

namespace MlbbOverlay.Services;

/// <summary>Copies images into the app's folders and deletes them again.</summary>
public class ImageStore
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp"];

    public static bool IsImage(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>"Yi Sun-shin" becomes "Yi_Sun-shin" (safe for file names).</summary>
    public static string ToSafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray();
        return new string(chars);
    }

    /// <summary>Copies a hero image in as "{Name}_{kind}.ext" and returns the new file name.</summary>
    public string SaveHeroImage(string sourcePath, string heroName, string kind)
    {
        var fileName = $"{ToSafeName(heroName)}_{kind}{Path.GetExtension(sourcePath).ToLowerInvariant()}";
        var target = Path.Combine(AppPaths.HeroImages, fileName);

        // If the file is already in the right place, there is nothing to copy.
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(target),
                           StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, target, overwrite: true);
        }
        return fileName;
    }

    public void DeleteHeroImage(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        var path = Path.Combine(AppPaths.HeroImages, fileName);
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* file in use: ignore, harmless leftover */ }
    }

    /// <summary>Copies a team logo in under a unique name and returns the file name.</summary>
    public string SaveLogo(string sourcePath)
    {
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(sourcePath).ToLowerInvariant()}";
        File.Copy(sourcePath, Path.Combine(AppPaths.LogoImages, fileName), overwrite: true);
        return fileName;
    }
}