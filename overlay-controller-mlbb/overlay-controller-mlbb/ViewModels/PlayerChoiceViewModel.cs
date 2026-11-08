using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;

namespace MlbbOverlay.ViewModels;

/// <summary>The "choose a player" window. The Blue team is on the left, Red on the right.</summary>
public partial class PlayerChoiceViewModel : ObservableObject
{
    public string Title { get; }
    public bool AskKillCount { get; }
    public IReadOnlyList<int> KillCounts { get; } = new[] { 2, 3, 4, 5 };

    public string LeftTitle { get; }
    public string RightTitle { get; }
    public IReadOnlyList<PlayerChoiceItem> LeftPlayers { get; }
    public IReadOnlyList<PlayerChoiceItem> RightPlayers { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChoose))]
    private int? _selectedKillCount;

    /// <summary>For kills, the number must be chosen before a player.</summary>
    public bool CanChoose => !AskKillCount || SelectedKillCount != null;

    public PlayerChoice? Result { get; private set; }

    public event Action<bool>? CloseRequested;

    public PlayerChoiceViewModel(string title, MatchState state, bool askKillCount)
    {
        Title = title;
        AskKillCount = askKillCount;

        var blue = state.BlueIsTeam1 ? 1 : 2;
        var red = 3 - blue;
        LeftTitle = $"{state.TeamOf(blue).Name}  (BLUE)";
        RightTitle = $"{state.TeamOf(red).Name}  (RED)";
        LeftPlayers = BuildPlayers(state, blue);
        RightPlayers = BuildPlayers(state, red);
    }

    private static List<PlayerChoiceItem> BuildPlayers(MatchState state, int teamIndex)
    {
        var team = state.TeamOf(teamIndex);
        var picks = state.PicksOf(teamIndex);
        var list = new List<PlayerChoiceItem>();

        for (var i = 0; i < 5; i++)
        {
            var ign = i < team.Players.Count && !string.IsNullOrWhiteSpace(team.Players[i])
                ? team.Players[i]
                : $"Player {i + 1}";
            var hero = i < picks.Count ? picks[i] : null;
            list.Add(new PlayerChoiceItem(teamIndex, i, ign, hero?.IconFile, hero?.Name ?? ""));
        }
        return list;
    }

    [RelayCommand]
    private void Choose(PlayerChoiceItem? item)
    {
        if (item == null || !CanChoose) return;
        Result = new PlayerChoice(item.TeamIndex, item.PlayerIndex, SelectedKillCount ?? 0);
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}