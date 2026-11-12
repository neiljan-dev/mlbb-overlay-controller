using Microsoft.EntityFrameworkCore;
using MlbbOverlay.Data;
using MlbbOverlay.Models;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MlbbOverlay.Services;

/// <summary>Saves and loads the overlays designed in the editor. (Overlay packs come in Milestone 9C.)</summary>
public class OverlayService
{
    public List<OverlayDefinition> GetAll()
    {
        using var db = new AppDbContext();
        return db.OverlayDefinitions.AsNoTracking().OrderBy(o => o.Key).ToList();
    }

    /// <summary>The saved design (JSON text) of an overlay, or null if it was never designed.</summary>
    public string? GetDesignJson(string key)
    {
        using var db = new AppDbContext();
        return db.OverlayDefinitions.AsNoTracking().FirstOrDefault(o => o.Key == key)?.DesignJson;
    }

    public void Save(string key, string displayName, string layer, string designJson)
    {
        using var _ = JsonDocument.Parse(designJson);   // throws if it is not valid JSON

        using var db = new AppDbContext();
        var row = db.OverlayDefinitions.FirstOrDefault(o => o.Key == key);
        if (row == null)
        {
            row = new OverlayDefinition { Key = key };
            db.OverlayDefinitions.Add(row);
        }

        row.DisplayName = displayName;
        row.Layer = layer;
        row.DesignJson = designJson;
        row.UpdatedAt = DateTime.UtcNow;
        db.SaveChanges();
    }

    public void Delete(string key)
    {
        using var db = new AppDbContext();
        var row = db.OverlayDefinitions.FirstOrDefault(o => o.Key == key);
        if (row == null) return;

        db.OverlayDefinitions.Remove(row);
        db.SaveChanges();
    }

    /// <summary>Is there a hand-coded file in wwwroot/handcoded for this overlay?</summary>
    public static bool HasHandcodedFile(string key)
    {
        var folder = Path.Combine(AppPaths.WebRoot, "handcoded");
        if (File.Exists(Path.Combine(folder, key + ".html"))) return true;

        // kill.2 ... kill.5 share kill.html
        return Regex.IsMatch(key, @"^kill\.\d+$") && File.Exists(Path.Combine(folder, "kill.html"));
    }
}