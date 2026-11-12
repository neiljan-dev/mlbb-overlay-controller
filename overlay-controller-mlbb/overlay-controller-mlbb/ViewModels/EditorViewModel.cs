using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

/// <summary>The editor screen: a top bar around the editor page (shown in WebView2).</summary>
public partial class EditorViewModel : ObservableObject
{
    private readonly Navigator _navigator;
    private readonly OverlayService _overlays;
    private readonly WebServerHost _server;
    private readonly MatchStateService _match;
    private readonly GameActionService _actions;
    private readonly DialogService _dialogs;

    public string Key { get; }
    public string Title { get; }
    public string Layer { get; }

    public string EditorUrl => $"http://127.0.0.1:{_server.Port}/editor?key={Uri.EscapeDataString(Key)}";

    /// <summary>Set by the view, so this class can read the design out of the editor page.</summary>
    public IEditorBridge? Bridge { get; set; }

    [ObservableProperty]
    private string _message = "";

    /// <summary>Test mode: "Play in OBS" fills the overlay with dummy data.</summary>
    public bool TestMode
    {
        get => _match.State.TestMode;
        set
        {
            _match.State.TestMode = value;
            OnPropertyChanged();
        }
    }

    public EditorViewModel(string key, string title, string layer, Navigator navigator,
                           OverlayService overlays, WebServerHost server, MatchStateService match,
                           GameActionService actions, DialogService dialogs)
    {
        Key = key;
        Title = title;
        Layer = layer;
        _navigator = navigator;
        _overlays = overlays;
        _server = server;
        _match = match;
        _actions = actions;
        _dialogs = dialogs;
    }

    private async Task<bool> SaveAsync()
    {
        if (Bridge == null)
        {
            Message = "The editor is not ready yet.";
            return false;
        }

        try
        {
            var json = await Bridge.GetDesignJsonAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                Message = "The editor is not ready yet.";
                return false;
            }

            _overlays.Save(Key, Title, Layer, json);
            await Bridge.MarkCleanAsync();
            Message = "Saved.";
            return true;
        }
        catch (Exception ex)
        {
            Message = "Could not save: " + ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private async Task Save() => await SaveAsync();

    [RelayCommand]
    private async Task SaveAndContinue()
    {
        if (await SaveAsync()) _navigator.Navigate<DesignListViewModel>();
    }

    [RelayCommand]
    private async Task PlayInObs()
    {
        if (!await SaveAsync()) return;

        try
        {
            await _actions.PlayOverlayAsync(new OverlayInfo(Key, Title, Layer));
            Message = TestMode
                ? "Playing with dummy data."
                : "Playing with the real match data (tick Test mode for dummy data).";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    [RelayCommand]
    private async Task Back()
    {
        if (Bridge != null)
        {
            try
            {
                if (await Bridge.IsDirtyAsync() &&
                    !_dialogs.Confirm("You have unsaved changes. Leave without saving?", "Unsaved changes"))
                    return;
            }
            catch
            {
                // if the editor can't answer, just leave
            }
        }
        _navigator.Navigate<DesignListViewModel>();
    }
}