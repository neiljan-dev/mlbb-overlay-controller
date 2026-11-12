using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using MlbbOverlay.Models;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

/// <summary>The Controller screen. Section 1 = Hero Selection, 2 = Game State, 3 = Overlay Animation.</summary>
public partial class ControllerViewModel : ObservableObject, IRecipient<MatchChangedMessage>
{
    private readonly Navigator _navigator;
    private readonly MatchStateService _match;
    private readonly GameActionService _actions;
    private readonly DialogService _dialogs;
    private readonly IServiceProvider _provider;

    public HeroSelectionViewModel HeroSelection { get; }
    public GameStateViewModel GameState { get; }
    public OverlayTesterViewModel OverlayTester { get; }
    public HotkeyService Hotkeys { get; }
    public string MatchSummary { get; }

    /// <summary>A red warning while test mode is on, so you never forget it during a live stream.</summary>
    public string TestModeBanner => _match.State.TestMode ? "TEST MODE: overlays use dummy data" : "";

    public ControllerViewModel(Navigator navigator, HeroSelectionViewModel heroSelection,
                               GameStateViewModel gameState, OverlayTesterViewModel overlayTester,
                               MatchStateService match, HotkeyService hotkeys, GameActionService actions,
                               DialogService dialogs, IServiceProvider provider)
    {
        _navigator = navigator;
        _match = match;
        _actions = actions;
        _dialogs = dialogs;
        _provider = provider;

        HeroSelection = heroSelection;
        GameState = gameState;
        OverlayTester = overlayTester;
        Hotkeys = hotkeys;

        var state = match.State;
        MatchSummary = $"{state.Team1.Name}  vs  {state.Team2.Name}   |   {state.Format}";

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(MatchChangedMessage message) => OnPropertyChanged(nameof(TestModeBanner));

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