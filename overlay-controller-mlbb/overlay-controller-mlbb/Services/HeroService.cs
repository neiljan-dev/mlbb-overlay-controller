using Microsoft.EntityFrameworkCore;
using MlbbOverlay.Data;
using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>What to do with one image when saving: use a new file, clear it, or (both empty) keep it.</summary>
public record ImageChange(string? NewSourcePath, bool Clear);

public class HeroService
{
    private readonly ImageStore _images;

    public HeroService(ImageStore images)
    {
        _images = images;
    }

    public List<Hero> GetAll(string? search = null)
    {
        using var db = new AppDbContext();
        IQueryable<Hero> query = db.Heroes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            query = query.Where(h => EF.Functions.Like(h.Name, $"%{text}%"));
        }
        return query.OrderBy(h => h.Name).ToList();
    }

    public int Count()
    {
        using var db = new AppDbContext();
        return db.Heroes.Count();
    }

    public bool NameExists(string name, int exceptId)
    {
        using var db = new AppDbContext();
        var n = name.Trim().ToLower();
        return db.Heroes.Any(h => h.Name.ToLower() == n && h.Id != exceptId);
    }

    /// <summary>Adds a hero (Id = 0) or updates an existing one.</summary>
    public void Save(Hero hero, ImageChange icon, ImageChange splash, ImageChange fullBody)
    {
        using var db = new AppDbContext();

        Hero entity;
        if (hero.Id == 0)
        {
            entity = new Hero();
            db.Heroes.Add(entity);
        }
        else
        {
            entity = db.Heroes.Find(hero.Id) ?? throw new InvalidOperationException("Hero no longer exists.");
        }

        entity.Name = hero.Name.Trim();
        entity.Role = hero.Role;
        entity.IconFile = Apply(entity.IconFile, icon, entity.Name, "icon");
        entity.SplashFile = Apply(entity.SplashFile, splash, entity.Name, "splash");
        entity.FullBodyFile = Apply(entity.FullBodyFile, fullBody, entity.Name, "full");

        db.SaveChanges();
    }

    public void Delete(int id)
    {
        using var db = new AppDbContext();
        var hero = db.Heroes.Find(id);
        if (hero == null) return;

        _images.DeleteHeroImage(hero.IconFile);
        _images.DeleteHeroImage(hero.SplashFile);
        _images.DeleteHeroImage(hero.FullBodyFile);

        db.Heroes.Remove(hero);
        db.SaveChanges();
    }

    private string? Apply(string? current, ImageChange change, string heroName, string kind)
    {
        if (change.NewSourcePath != null)
        {
            var saved = _images.SaveHeroImage(change.NewSourcePath, heroName, kind);
            if (current != null && !current.Equals(saved, StringComparison.OrdinalIgnoreCase))
                _images.DeleteHeroImage(current);
            return saved;
        }
        if (change.Clear)
        {
            _images.DeleteHeroImage(current);
            return null;
        }
        return current;
    }
}