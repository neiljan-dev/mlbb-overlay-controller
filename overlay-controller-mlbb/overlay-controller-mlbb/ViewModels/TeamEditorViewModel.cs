using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;
using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace MlbbOverlay.ViewModels;

/// <summary>One line in the player list ("Player 1" + the IGN box).</summary>
public partial class PlayerEntry : ObservableObject
{
    public string Label { get; }

    [ObservableProperty]
    private string _ign = "";

    public PlayerEntry(string label)
    {
        Label = label;
    }
}

/// <summary>One team panel (logo, name, 5 players, saved-team list). Used twice on the Team Setup screen.</summary>
public partial class TeamEditorViewModel : ObservableObject
{
    private readonly ProfileService _profiles;
    private readonly ImageStore _images;
    private readonly DialogService _dialogs;

    private int? _profileId;
    private bool _suppressLoad;
    private string? _pendingSelect;

    public string Title { get; }
    public ObservableCollection<PlayerEntry> Players { get; } = new();
    public ObservableCollection<Team> SavedTeams { get; } = new();

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string? _logoFile;
    [ObservableProperty] private Team? _selectedSavedTeam;

    /// <summary>Raised after a team was saved or deleted, so the parent can refresh both panels.</summary>
    public event Action? SavedTeamsChanged;

    public TeamEditorViewModel(string title, ProfileService profiles, ImageStore images, DialogService dialogs)
    {
        Title = title;
        _profiles = profiles;
        _images = images;
        _dialogs = dialogs;

        for (var i = 1; i <= 5; i++) Players.Add(new PlayerEntry($"Player {i}"));
    }

    public void SetProfile(int? profileId)
    {
        _profileId = profileId;
        SelectedSavedTeam = null;
        RefreshSavedTeams();
    }

    /// <summary>Reloads the saved-team list without touching what is typed in the panel.</summary>
    public void RefreshSavedTeams()
    {
        var keep = _pendingSelect ?? SelectedSavedTeam?.Name;
        _pendingSelect = null;

        _suppressLoad = true;
        try
        {
            SavedTeams.Clear();
            if (_profileId != null)
            {
                foreach (var team in _profiles.GetTeams(_profileId.Value)) SavedTeams.Add(team);
            }
            SelectedSavedTeam = keep == null
                ? null
                : SavedTeams.FirstOrDefault(t => t.Name.Equals(keep, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _suppressLoad = false;
        }
    }

    // Choosing a saved team copies it into the panel.
    partial void OnSelectedSavedTeamChanged(Team? value)
    {
        if (_suppressLoad || value == null) return;

        Name = value.Name;
        LogoFile = value.LogoFile;
        var ordered = value.Players.OrderBy(p => p.Order).ToList();
        for (var i = 0; i < Players.Count; i++)
            Players[i].Ign = i < ordered.Count ? ordered[i].Ign : "";
    }

    public TeamSnapshot ToSnapshot() => new()
    {
        Name = Name.Trim(),
        LogoFile = LogoFile,
        Players = Players.Select(p => p.Ign.Trim()).ToList()
    };

    public void LoadSnapshot(TeamSnapshot snapshot)
    {
        Name = snapshot.Name;
        LogoFile = snapshot.LogoFile;
        for (var i = 0; i < Players.Count; i++)
            Players[i].Ign = i < snapshot.Players.Count ? snapshot.Players[i] : "";
    }

    [RelayCommand]
    private void BrowseLogo()
    {
        var file = _dialogs.PickImage();
        if (file == null) return;

        try { LogoFile = _images.SaveLogo(file); }
        catch (Exception ex) { _dialogs.Error(ex.Message); }
    }

    [RelayCommand]
    private void ClearLogo() => LogoFile = null;

    [RelayCommand]
    private void SaveToProfile()
    {
        if (_profileId == null) { _dialogs.Info("Select or create a profile first."); return; }
        if (string.IsNullOrWhiteSpace(Name)) { _dialogs.Info("Type a team name first."); return; }

        try
        {
            var saved = _profiles.SaveTeam(_profileId.Value, ToSnapshot());
            _pendingSelect = saved.Name;
            SavedTeamsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _dialogs.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void DeleteSaved()
    {
        if (SelectedSavedTeam == null) { _dialogs.Info("Pick a saved team in the list first."); return; }
        if (!_dialogs.Confirm($"Delete the saved team \"{SelectedSavedTeam.Name}\" from this profile?")) return;

        try
        {
            _profiles.DeleteTeam(SelectedSavedTeam.Id);
            SavedTeamsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _dialogs.Error(ex.Message);
        }
    }
}