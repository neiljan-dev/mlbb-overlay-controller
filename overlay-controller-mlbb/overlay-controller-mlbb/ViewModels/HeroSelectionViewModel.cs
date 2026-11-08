using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

/// <summary>Section 1 of the Controller: pick and ban heroes for both teams.</summary>
public partial class HeroSelectionViewModel : ObservableObject
{
    private readonly MatchStateService _match;
    private readonly HeroService _heroes;
    private readonly DialogService _dialogs;
    private readonly OverlayDirector _director;

    public TeamDraftViewModel Team1 { get; }
    public TeamDraftViewModel Team2 { get; }
    public IEnumerable<int> BanCounts { get; } = Enumerable.Range(0, DraftState.MaxBans + 1).ToList();

    [ObservableProperty]
    private int _bansPerTeam;

    public HeroSelectionViewModel(MatchStateService match, HeroService heroes,
                                  DialogService dialogs, OverlayDirector director)
    {
        _match = match;
        _heroes = heroes;
        _dialogs = dialogs;
        _director = director;

        var state = _match.State;
        var draft = state.Draft;
        _bansPerTeam = Math.Clamp(draft.BansPerTeam, 0, DraftState.MaxBans);

        Team1 = new TeamDraftViewModel($"TEAM 1   |   {state.Team1.Name}", state.Team1,
                                       draft.Picks1, draft.Bans1, OnPick, OnClear);
        Team2 = new TeamDraftViewModel($"TEAM 2   |   {state.Team2.Name}", state.Team2,
                                       draft.Picks2, draft.Bans2, OnPick, OnClear);
        Team1.RebuildBans(_bansPerTeam);
        Team2.RebuildBans(_bansPerTeam);
    }

    // The "bans per team" box changed
    partial void OnBansPerTeamChanged(int value)
    {
        var draft = _match.State.Draft;
        draft.BansPerTeam = value;

        // Bans that no longer exist are removed
        for (var i = value; i < DraftState.MaxBans; i++)
        {
            draft.Bans1[i] = null;
            draft.Bans2[i] = null;
        }

        Team1.RebuildBans(value);
        Team2.RebuildBans(value);
        Changed();
    }

    private void OnPick(DraftSlotViewModel slot)
    {
        var hero = _dialogs.ShowHeroPicker(new HeroPickerViewModel(_heroes));
        if (hero == null) return;

        var usedBy = FindUsage(hero.Id, slot);
        if (usedBy != null)
        {
            _dialogs.Info($"{hero.Name} is already {usedBy}.", "Hero not available");
            return;
        }

        slot.SetHero(HeroSnapshot.From(hero));
        Changed();
    }

    private void OnClear(DraftSlotViewModel slot)
    {
        if (slot.Picked == null) return;
        slot.SetHero(null);
        Changed();
    }

    // A hero can only be picked or banned once per game
    private string? FindUsage(int heroId, DraftSlotViewModel except)
    {
        foreach (var (team, label) in new[] { (Team1, "TEAM 1"), (Team2, "TEAM 2") })
        {
            foreach (var slot in team.Picks)
                if (!ReferenceEquals(slot, except) && slot.Picked?.Id == heroId)
                    return $"picked by {label} ({slot.Ign})";

            foreach (var slot in team.Bans)
                if (!ReferenceEquals(slot, except) && slot.Picked?.Id == heroId)
                    return $"banned by {label}";
        }
        return null;
    }

    [RelayCommand]
    private void ResetDraft()
    {
        if (!_dialogs.Confirm("Clear all picks and bans?")) return;

        foreach (var slot in Team1.AllSlots.Concat(Team2.AllSlots).ToList())
            slot.SetHero(null);
        Changed();
    }

    // Tell whatever is on screen that the data changed
    private void Changed() => _ = _director.UpdateDataAsync();
}