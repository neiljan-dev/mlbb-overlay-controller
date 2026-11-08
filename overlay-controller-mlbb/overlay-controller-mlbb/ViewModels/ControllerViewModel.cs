using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

/// <summary>The Controller screen. Section 1 = Hero Selection, 2 = Game State, 3 = Overlay Animation (Milestone 8).</summary>
public partial class ControllerViewModel : ObservableObject
{
    private readonly Navigator _navigator;
    private readonly GameActionService _actions;
    private readonly DialogService _dialogs;
    private readonly IServiceProvider _provider;

    public HeroSelectionViewModel HeroSelection { get; }
    public GameStateViewModel GameState { get; }
    public HotkeyService Hotkeys { get; }
    public string MatchSummary { get; }

    public ControllerViewModel(Navigator navigator, HeroSelectionViewModel heroSelection,
                               GameStateViewModel gameState, MatchStateService match,
                               HotkeyService hotkeys, GameActionService actions,
                               DialogService dialogs, IServiceProvider provider)
    {
        _navigator = navigator;
        _actions = actions;
        _dialogs = dialogs;
        _provider = provider;

        HeroSelection = heroSelection;
        GameState = gameState;
        Hotkeys = hotkeys;

        var state = match.State;
        MatchSummary = $"{state.Team1.Name}  vs  {state.Team2.Name}   |   {state.Format}";
    }

    private async Task Run(Func<Task> action, string message)
    {
        try
        {
            await action();
            Hotkeys.Flash(message);
        }
        catch (Exception ex)
        {
            _dialogs.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task HideAll() => await Run(() => _actions.PanicAsync(), "All overlays hidden. (Restore brings the overlay back.)");

    [RelayCommand]
    private async Task CancelLast() => await Run(() => _actions.CancelLastAsync(), "Cancelled the last overlay.");

    [RelayCommand]
    private async Task Restore() => await Run(() => _actions.RestoreAsync(), "Overlay restored.");

    [RelayCommand]
    private void OpenSettings() =>
        _dialogs.ShowControllerSettings(_provider.GetRequiredService<ControllerSettingsViewModel>());

    [RelayCommand]
    private void Back() => _navigator.Navigate<TeamSetupViewModel>();
}