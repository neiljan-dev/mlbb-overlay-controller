using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MlbbOverlay.Models;

namespace MlbbOverlay.ViewModels;

/// <summary>One team's strip: 5 pick slots and a few ban slots.</summary>
public class TeamDraftViewModel : ObservableObject
{
    private readonly List<HeroSnapshot?> _banStore;
    private readonly Action<DraftSlotViewModel> _pick;
    private readonly Action<DraftSlotViewModel> _clear;

    public string Title { get; }
    public ObservableCollection<DraftSlotViewModel> Picks { get; } = new();
    public ObservableCollection<DraftSlotViewModel> Bans { get; } = new();

    public IEnumerable<DraftSlotViewModel> AllSlots => Picks.Concat(Bans);

    public TeamDraftViewModel(string title, TeamSnapshot team,
                              List<HeroSnapshot?> pickStore, List<HeroSnapshot?> banStore,
                              Action<DraftSlotViewModel> pick, Action<DraftSlotViewModel> clear)
    {
        Title = title;
        _banStore = banStore;
        _pick = pick;
        _clear = clear;

        for (var i = 0; i < 5; i++)
        {
            var ign = i < team.Players.Count && !string.IsNullOrWhiteSpace(team.Players[i])
                ? team.Players[i]
                : $"Player {i + 1}";
            Picks.Add(new DraftSlotViewModel(pickStore, i, false, ign, pick, clear));
        }
    }

    /// <summary>Creates the ban circles (called at the start and whenever "bans per team" changes).</summary>
    public void RebuildBans(int count)
    {
        Bans.Clear();
        for (var i = 0; i < count; i++)
            Bans.Add(new DraftSlotViewModel(_banStore, i, true, "", _pick, _clear));
    }
}