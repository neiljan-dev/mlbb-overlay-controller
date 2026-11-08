using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

/// <summary>Section 2 of the Controller: the state switch and the buttons of each state.</summary>
public partial class GameStateViewModel : ObservableObject
{
    private readonly MatchStateService _match;
    private readonly GameActionService _actions;
    private readonly DialogService _dialogs;

    public IReadOnlyList<StateOption> States { get; } = new[]
    {
        new StateOption(GameState.DraftPick, "Draft Pick"),
        new StateOption(GameState.Ingame, "Ingame"),
        new StateOption(GameState.Postgame, "Postgame"),
        new StateOption(GameState.Neiljan, "Neiljan")
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDraft), nameof(IsIngame), nameof(IsPostgame), nameof(IsNeiljan))]
    private GameState _selectedState;

    public bool IsDraft => SelectedState == GameState.DraftPick;
    public bool IsIngame => SelectedState == GameState.Ingame;
    public bool IsPostgame => SelectedState == GameState.Postgame;
    public bool IsNeiljan => SelectedState == GameState.Neiljan;

    public GameStateViewModel(MatchStateService match, GameActionService actions,
                              DialogService dialogs, HotkeyService hotkeys)
    {
        _match = match;
        _actions = actions;
        _dialogs = dialogs;

        _selectedState = match.State.CurrentState;
        _ = Run(() => _actions.EnsureStateOverlayAsync());

        // Hotkeys do the same as clicking the buttons
        hotkeys.SetHandlers(new Dictionary<string, Action>
        {
            ["state.draft"] = () => SelectedState = GameState.DraftPick,
            ["state.ingame"] = () => SelectedState = GameState.Ingame,
            ["state.postgame"] = () => SelectedState = GameState.Postgame,
            ["state.neiljan"] = () => SelectedState = GameState.Neiljan,

            ["ingame.turtleSpawn"] = () => TurtleSpawnCommand.Execute(null),
            ["ingame.lordSpawn"] = () => LordSpawnCommand.Execute(null),
            ["ingame.kill"] = () => KillCommand.Execute(null),
            ["ingame.victory"] = () => VictoryCommand.Execute(null),

            ["postgame.result"] = () => ResultCommand.Execute(null),
            ["postgame.mvp"] = () => MvpCommand.Execute(null),
            ["postgame.status"] = () => StatusCommand.Execute(null),

            ["neiljan.ad1"] = () => Ad1Command.Execute(null),
            ["neiljan.ad2"] = () => Ad2Command.Execute(null),
            ["neiljan.ad3"] = () => Ad3Command.Execute(null),
        });
    }

    // The state switch changed: show the overlay of the new state
    partial void OnSelectedStateChanged(GameState value) => _ = Run(() => _actions.SetStateAsync(value));

    private async Task Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { _dialogs.Error(ex.Message); }
    }

    // ---------- Dialog helpers ----------

    private PlayerChoice? ChoosePlayer(string title, bool askKillCount) =>
        _dialogs.ShowPlayerChoice(new PlayerChoiceViewModel(title, _match.State, askKillCount));

    private int? ChooseTeam(string title)
    {
        var blue = _match.State.BlueIsTeam1 ? 1 : 2;
        return _dialogs.ShowTeamChoice(new TeamChoiceModel(title, TeamItem(blue), TeamItem(3 - blue)));
    }

    private TeamChoiceItem TeamItem(int teamIndex)
    {
        var state = _match.State;
        var team = state.TeamOf(teamIndex);
        var isBlue = state.SideOf(teamIndex) == "blue";
        return new TeamChoiceItem(teamIndex, team.Name, team.LogoFile,
                                  isBlue ? "BLUE SIDE" : "RED SIDE",
                                  isBlue ? "#5AA9FF" : "#FF6B6B");
    }

    // ---------- Draft Pick ----------

    [RelayCommand]
    private async Task ShowDraftOverlay() => await Run(() => _actions.SetStateAsync(GameState.DraftPick));

    // ---------- Ingame ----------

    [RelayCommand]
    private async Task TurtleSlain()
    {
        var player = ChoosePlayer("Turtle slain: who got it?", false);
        if (player != null) await Run(() => _actions.TurtleSlainAsync(player.TeamIndex, player.PlayerIndex));
    }

    [RelayCommand]
    private async Task LordSlain()
    {
        var player = ChoosePlayer("Lord slain: who got it?", false);
        if (player != null) await Run(() => _actions.LordSlainAsync(player.TeamIndex, player.PlayerIndex));
    }

    [RelayCommand]
    private async Task TurtleSpawn() => await Run(() => _actions.TurtleSpawnAsync());

    [RelayCommand]
    private async Task LordSpawn() => await Run(() => _actions.LordSpawnAsync());

    [RelayCommand]
    private async Task Kill()
    {
        var player = ChoosePlayer("Kill: how many, and who?", true);
        if (player != null)
            await Run(() => _actions.KillAsync(player.TeamIndex, player.PlayerIndex, player.KillCount));
    }

    [RelayCommand]
    private async Task Victory()
    {
        var team = ChooseTeam("Victory: which team won?");
        if (team != null) await Run(() => _actions.VictoryAsync(team.Value));
    }

    // ---------- Postgame ----------

    [RelayCommand]
    private async Task Result()
    {
        var team = ChooseTeam("Result: which team?");
        if (team != null) await Run(() => _actions.ResultAsync(team.Value));
    }

    [RelayCommand]
    private async Task Mvp()
    {
        var player = ChoosePlayer("MVP: which player?", false);
        if (player != null) await Run(() => _actions.MvpAsync(player.TeamIndex, player.PlayerIndex));
    }

    [RelayCommand]
    private async Task Status()
    {
        var state = _match.State;
        var scores = _dialogs.ShowNumbers("Status score",
            $"{state.Team1.Name} score", state.StatusScore1,
            $"{state.Team2.Name} score", state.StatusScore2);
        if (scores != null) await Run(() => _actions.StatusAsync(scores.Value.First, scores.Value.Second));
    }

    // ---------- Neiljan (ads) ----------

    [RelayCommand]
    private async Task Ad1()
    {
        var minutes = _dialogs.ShowNumbers("Ad 1", "Timer in minutes", 5, min: 1);
        if (minutes != null) await Run(() => _actions.AdAsync(1, minutes.Value.First));
    }

    [RelayCommand]
    private async Task Ad2()
    {
        var minutes = _dialogs.ShowNumbers("Ad 2", "Timer in minutes", 5, min: 1);
        if (minutes != null) await Run(() => _actions.AdAsync(2, minutes.Value.First));
    }

    [RelayCommand]
    private async Task Ad3() => await Run(() => _actions.Ad3Async());
}