using MlbbOverlay.Data;
using MlbbOverlay.Models;
using System.IO;

namespace MlbbOverlay.Services;

public class BulkImportResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public List<string> Skipped { get; } = new();
}

/// <summary>
/// Imports a folder of images named like Layla_icon.png, Layla_splash.png, Layla_full.png.
/// Only the LAST underscore separates the name from the type (icon / splash / full),
/// so names like "Yi_Sun-shin_icon.png" work too.
/// </summary>
public class BulkImportService
{
    private static readonly string[] Kinds = ["icon", "splash", "full"];
    private readonly ImageStore _images;

    public BulkImportService(ImageStore images)
    {
        _images = images;
    }

    private class Group
    {
        public string DisplayName = "";
        public Dictionary<string, string> Files = new();
    }

    // "Chang'e", "Chang_e" and "chang e" all become "change", so they match the same hero.
    private static string Normalize(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    public BulkImportResult Import(string folder)
    {
        var result = new BulkImportResult();
        var groups = new Dictionary<string, Group>();

        // 1) Read the file names and group them by hero
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            if (!ImageStore.IsImage(file)) continue;

            var fileName = Path.GetFileName(file);
            var stem = Path.GetFileNameWithoutExtension(file);
            var cut = stem.LastIndexOf('_');
            if (cut <= 0) { result.Skipped.Add(fileName); continue; }

            var namePart = stem[..cut];
            var kind = stem[(cut + 1)..].ToLowerInvariant();
            if (kind == "fullbody") kind = "full";
            if (!Kinds.Contains(kind)) { result.Skipped.Add(fileName); continue; }

            var key = Normalize(namePart);
            if (key.Length == 0) { result.Skipped.Add(fileName); continue; }

            if (!groups.TryGetValue(key, out var group))
            {
                group = new Group { DisplayName = namePart.Replace('_', ' ').Trim() };
                groups[key] = group;
            }
            group.Files[kind] = file;
        }

        // 2) Create or update the heroes
        using var db = new AppDbContext();
        var existing = db.Heroes.ToList();

        foreach (var (key, group) in groups)
        {
            var hero = existing.FirstOrDefault(h => Normalize(h.Name) == key);
            var isNew = hero == null;

            if (hero == null)
            {
                hero = new Hero { Name = group.DisplayName, Role = HeroRole.Unassigned };
                db.Heroes.Add(hero);
                existing.Add(hero);
            }

            foreach (var (kind, file) in group.Files)
            {
                var saved = _images.SaveHeroImage(file, hero.Name, kind);
                switch (kind)
                {
                    case "icon": hero.IconFile = Replace(hero.IconFile, saved); break;
                    case "splash": hero.SplashFile = Replace(hero.SplashFile, saved); break;
                    case "full": hero.FullBodyFile = Replace(hero.FullBodyFile, saved); break;
                }
            }

            if (isNew) result.Created++; else result.Updated++;
        }

        db.SaveChanges();
        return result;
    }

    private string Replace(string? old, string saved)
    {
        if (old != null && !old.Equals(saved, StringComparison.OrdinalIgnoreCase))
            _images.DeleteHeroImage(old);
        return saved;
    }
}