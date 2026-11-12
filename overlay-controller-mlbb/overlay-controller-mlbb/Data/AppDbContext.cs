using System.IO;
using Microsoft.EntityFrameworkCore;
using MlbbOverlay.Models;
using MlbbOverlay.Services;

namespace MlbbOverlay.Data;

public class AppDbContext : DbContext
{
    public DbSet<Hero> Heroes => Set<Hero>();
    public DbSet<TournamentProfile> Profiles => Set<TournamentProfile>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<OverlayDefinition> OverlayDefinitions => Set<OverlayDefinition>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        Directory.CreateDirectory(AppPaths.Root);
        options.UseSqlite($"Data Source={AppPaths.Database}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hero>(e =>
        {
            e.Property(h => h.Name).IsRequired().HasMaxLength(60);
            e.Property(h => h.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<TournamentProfile>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(60);
        });

        modelBuilder.Entity<Team>(e =>
        {
            e.Property(t => t.Name).IsRequired().HasMaxLength(60);
            e.HasOne<TournamentProfile>().WithMany(p => p.Teams)
                .HasForeignKey(t => t.ProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Player>(e =>
        {
            e.Property(p => p.Ign).HasMaxLength(40);
            e.HasOne<Team>().WithMany(t => t.Players)
                .HasForeignKey(p => p.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OverlayDefinition>(e =>
        {
            e.HasIndex(o => o.Key).IsUnique();
            e.Property(o => o.Key).IsRequired().HasMaxLength(80);
            e.Property(o => o.DisplayName).HasMaxLength(100);
        });
    }
}