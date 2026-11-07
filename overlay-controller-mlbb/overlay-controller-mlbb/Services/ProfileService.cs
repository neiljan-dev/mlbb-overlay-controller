using Microsoft.EntityFrameworkCore;
using MlbbOverlay.Data;
using MlbbOverlay.Models;

namespace MlbbOverlay.Services;

/// <summary>Profiles (events) and their saved teams.</summary>
public class ProfileService
{
    public List<TournamentProfile> GetProfiles()
    {
        using var db = new AppDbContext();
        return db.Profiles.AsNoTracking().OrderBy(p => p.Name).ToList();
    }

    public TournamentProfile CreateProfile(string name)
    {
        name = name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Please type a name.");

        using var db = new AppDbContext();
        var lower = name.ToLower();
        if (db.Profiles.Any(p => p.Name.ToLower() == lower))
            throw new InvalidOperationException("A profile with this name already exists.");

        var profile = new TournamentProfile { Name = name };
        db.Profiles.Add(profile);
        db.SaveChanges();
        return profile;
    }

    public void DeleteProfile(int id)
    {
        using var db = new AppDbContext();
        var profile = db.Profiles.Include(p => p.Teams).ThenInclude(t => t.Players)
                        .FirstOrDefault(p => p.Id == id);
        if (profile == null) return;

        foreach (var team in profile.Teams) db.Players.RemoveRange(team.Players);
        db.Teams.RemoveRange(profile.Teams);
        db.Profiles.Remove(profile);
        db.SaveChanges();
    }

    public List<Team> GetTeams(int profileId)
    {
        using var db = new AppDbContext();
        return db.Teams.AsNoTracking()
                 .Include(t => t.Players)
                 .Where(t => t.ProfileId == profileId)
                 .OrderBy(t => t.Name)
                 .ToList();
    }

    /// <summary>Saves a team into a profile. A team with the same name is updated, otherwise a new one is added.</summary>
    public Team SaveTeam(int profileId, TeamSnapshot snapshot)
    {
        using var db = new AppDbContext();

        var name = snapshot.Name.Trim();
        var lower = name.ToLower();
        var team = db.Teams.Include(t => t.Players)
                     .FirstOrDefault(t => t.ProfileId == profileId && t.Name.ToLower() == lower);

        if (team == null)
        {
            team = new Team { ProfileId = profileId };
            db.Teams.Add(team);
        }

        team.Name = name;
        team.LogoFile = snapshot.LogoFile;

        for (var i = 0; i < 5; i++)
        {
            var player = team.Players.FirstOrDefault(p => p.Order == i + 1);
            if (player == null)
            {
                player = new Player { Order = i + 1 };
                team.Players.Add(player);
            }
            player.Ign = i < snapshot.Players.Count ? snapshot.Players[i].Trim() : "";
        }

        db.SaveChanges();
        return team;
    }

    public void DeleteTeam(int teamId)
    {
        using var db = new AppDbContext();
        var team = db.Teams.Include(t => t.Players).FirstOrDefault(t => t.Id == teamId);
        if (team == null) return;

        db.Players.RemoveRange(team.Players);
        db.Teams.Remove(team);
        db.SaveChanges();
    }
}