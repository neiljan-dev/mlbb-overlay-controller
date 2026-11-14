using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MlbbOverlay.Models;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

/// <summary>Section 3 of the Controller: test mode and a Play button for every overlay (standard and custom).</summary>
public partial class OverlayTesterViewModel : ObservableObject, IRecipient<MatchChangedMessage>
{
    private readonly MatchStateService _match;
    private readonly GameActionService _actions;
    private readonly OverlayDirector _director;
    private readonly DialogService _dialogs;

    /// <summary>The standard overlays, then the ones you added in the editor.</summary>
    public IReadOnlyList<OverlayInfo> Overlays { get; }

    /// <summary>Test mode: every overlay is filled with dummy data.</summary>
    public bool TestMode
    {
        get => _match.State.TestMode;
        set
        {
            if (_match.State.TestMode == value) return;
            _match.State.TestMode = value;
            OnPropertyChanged();

            WeakReferenceMessenger.Default.Send(new MatchChangedMessage());   // updates the red warning
            _ = _director.UpdateDataAsync();                                  // refresh what is on screen
        }
    }

    public OverlayTesterViewModel(MatchStateService match, GameActionService actions,
                                  OverlayDirector director, DialogService dialogs,
                                  OverlayService overlays)
    {
        _match = match;
        _actions = actions;
        _director = director;
        _dialogs = dialogs;

        var list = OverlayCatalog.All.ToList();
        foreach (var definition in overlays.GetAll())
        {
            if (list.All(item => item.Key != definition.Key))
                list.Add(new OverlayInfo(definition.Key, definition.DisplayName + " (custom)", definition.Layer));
        }
        Overlays = list;

        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(MatchChangedMessage message) => OnPropertyChanged(nameof(TestMode));

    [RelayCommand]
    private async Task Play(OverlayInfo? info)
    {
        if (info == null) return;

        try { await _actions.PlayOverlayAsync(info); }
        catch (Exception ex) { _dialogs.Error(ex.Message); }
    }
}