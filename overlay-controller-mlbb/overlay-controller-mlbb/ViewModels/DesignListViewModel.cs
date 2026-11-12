using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

/// <summary>One line of the overlay list.</summary>
public record DesignListItem(string Key, string Label, string Layer, string Status, bool IsDesigned, bool IsCustom)
{
    public string LayerText => Layer == OverlayLayers.Base ? "Stays" : "Short (queue)";
    public string ResetText => IsCustom ? "Delete" : "Reset";
}

/// <summary>The Design x Animation screen: the list of all overlays.</summary>
public partial class DesignListViewModel : ObservableObject
{
    private readonly Navigator _navigator;
    private readonly OverlayService _overlays;
    private readonly WebServerHost _server;
    private readonly MatchStateService _match;
    private readonly GameActionService _actions;
    private readonly DialogService _dialogs;

    public ObservableCollection<DesignListItem> Items { get; } = new();

    public DesignListViewModel(Navigator navigator, OverlayService overlays, WebServerHost server,
                               MatchStateService match, GameActionService actions, DialogService dialogs)
    {
        _navigator = navigator;
        _overlays = overlays;
        _server = server;
        _match = match;
        _actions = actions;
        _dialogs = dialogs;

        Load();
    }

    private void Load()
    {
        Items.Clear();
        var saved = _overlays.GetAll().ToDictionary(o => o.Key);

        // The standard overlays first, then the ones you added yourself
        foreach (var info in OverlayCatalog.All)
            Items.Add(MakeItem(info.Key, info.Label, info.Layer, saved, false));

        foreach (var def in saved.Values.Where(d => OverlayCatalog.All.All(i => i.Key != d.Key)))
            Items.Add(MakeItem(def.Key, def.DisplayName, def.Layer, saved, true));
    }

    private static DesignListItem MakeItem(string key, string label, string layer,
                                           Dictionary<string, OverlayDefinition> saved, bool custom)
    {
        var designed = saved.ContainsKey(key);
        var status = designed ? "Designed in the editor"
                   : OverlayService.HasHandcodedFile(key) ? "Hand-coded file"
                   : "Not designed yet";
        return new DesignListItem(key, label, layer, status, designed, custom);
    }

    private void OpenEditor(string key, string label, string layer)
    {
        if (!_server.IsRunning)
        {
            _dialogs.Error("The overlay server is not running, so the editor can't load. Check the status bar at the top.");
            return;
        }

        _navigator.NavigateTo(new EditorViewModel(key, label, layer, _navigator, _overlays,
                                                  _server, _match, _actions, _dialogs));
    }

    [RelayCommand]
    private void Back() => _navigator.Navigate<MainMenuViewModel>();

    [RelayCommand]
    private void Edit(DesignListItem? item)
    {
        if (item != null) OpenEditor(item.Key, item.Label, item.Layer);
    }

    [RelayCommand]
    private void Add()
    {
        var key = _dialogs.Prompt("New overlay",
            "Overlay key (lowercase letters, digits, dots and dashes, for example intro.ov):");
        if (string.IsNullOrWhiteSpace(key)) return;
        key = key.Trim().ToLowerInvariant();

        if (!Regex.IsMatch(key, @"^[a-z0-9][a-z0-9._-]*$"))
        {
            _dialogs.Error("Use only lowercase letters, digits, dots, dashes and underscores.");
            return;
        }
        if (Items.Any(i => i.Key == key))
        {
            _dialogs.Error("An overlay with this key already exists.");
            return;
        }

        var name = _dialogs.Prompt("New overlay", "Display name:", key);
        if (string.IsNullOrWhiteSpace(name)) return;

        var isShort = _dialogs.Confirm(
            "Is this a short overlay?\n\nYes = short (plays through the queue).\nNo = stays on screen until replaced.",
            "Overlay type");

        OpenEditor(key, name.Trim(), isShort ? OverlayLayers.Event : OverlayLayers.Base);
    }

    [RelayCommand]
    private void Reset(DesignListItem? item)
    {
        if (item == null) return;

        var question = item.IsCustom
            ? $"Delete the overlay \"{item.Label}\"?"
            : $"Remove your design of \"{item.Label}\"?\n\nThe overlay goes back to its hand-coded file (or the placeholder card).";
        if (!_dialogs.Confirm(question, "Please confirm")) return;

        try
        {
            _overlays.Delete(item.Key);
            Load();
        }
        catch (Exception ex)
        {
            _dialogs.Error(ex.Message);
        }
    }
}