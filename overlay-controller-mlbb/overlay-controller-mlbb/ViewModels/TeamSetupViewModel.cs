using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;
using System.Collections.ObjectModel;
using System.DirectoryServices.ActiveDirectory;

namespace MlbbOverlay.ViewModels;

public partial class TeamSetupViewModel : ObservableObject
{
    private readonly Navigator _navigator;
    private readonly ProfileService _profiles;
    private readonly MatchStateService _match;
    private readonly DialogService _dialogs;

    public ObservableCollection<TournamentProfile> Profiles { get; } = new();
    public IEnumerable<SeriesFormat> Formats { get; } = Enum.GetValues<SeriesFormat>();

    public TeamEditorViewModel Team1 { get; }
    public TeamEditorViewModel Team2 { get; }

    [ObservableProperty] private TournamentProfile? _selectedProfile;
    [ObservableProperty] private SeriesFormat _format = SeriesFormat.BO3;
    [ObservableProperty] private string _errorText = "";

    public TeamSetupViewModel(Navigator navigator, ProfileService profiles, MatchStateService match,
                              ImageStore images, DialogService dialogs)
    {
        _navigator = navigator;
        _profiles = profiles;
        _match = match;
        _dialogs = dialogs;

        Team1 = new TeamEditorViewModel("TEAM 1   |   BLUE SIDE   |   picks first", profiles, images, dialogs);
        Team2 = new TeamEditorViewModel("TEAM 2   |   RED SIDE", profiles, images, dialogs);
        Team1.SavedTeamsChanged += OnSavedTeamsChanged;
        Team2.SavedTeamsChanged += OnSavedTeamsChanged;

        LoadProfiles(_match.State.ProfileId);

        // Show what was set up earlier in this session
        Format = _match.State.Format;
        Team1.LoadSnapshot(_match.State.Team1);
        Team2.LoadSnapshot(_match.State.Team2);
    }

    private void OnSavedTeamsChanged()
    {
        Team1.RefreshSavedTeams();
        Team2.RefreshSavedTeams();
    }

    private void LoadProfiles(int? selectId)
    {
        var list = _profiles.GetProfiles();
        if (list.Count == 0)
        {
            _profiles.CreateProfile("Default");
            list = _profiles.GetProfiles();
        }

        Profiles.Clear();
        foreach (var profile in list) Profiles.Add(profile);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == selectId) ?? Profiles.First();
    }

    partial void OnSelectedProfileChanged(TournamentProfile? value)
    {
        Team1.SetProfile(value?.Id);
        Team2.SetProfile(value?.Id);
    }

    [RelayCommand]
    private void NewProfile()
    {
        var name = _dialogs.Prompt("New profile", "Event / profile name:");
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var profile = _profiles.CreateProfile(name);
            LoadProfiles(profile.Id);
        }
        catch (Exception ex)
        {
            _dialogs.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile == null) return;
        if (!_dialogs.Confirm($"Delete the profile \"{SelectedProfile.Name}\" and all its saved teams?")) return;

        try
        {
            _profiles.DeleteProfile(SelectedProfile.Id);
            LoadProfiles(null);
        }
        catch (Exception ex)
        {
            _dialogs.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void Back()
    {
        // Keep what was typed, so it is still there when you come back
        _match.ApplyTeamSetup(SelectedProfile?.Id, Format, Team1.ToSnapshot(), Team2.ToSnapshot());
        _navigator.Navigate<MainMenuViewModel>();
    }

    [RelayCommand]
    private void Proceed()
    {
        var team1 = Team1.ToSnapshot();
        var team2 = Team2.ToSnapshot();

        if (string.IsNullOrWhiteSpace(team1.Name) || string.IsNullOrWhiteSpace(team2.Name))
        {
            ErrorText = "Both teams need a name.";
            return;
        }
        if (string.Equals(team1.Name, team2.Name, StringComparison.OrdinalIgnoreCase))
        {
            ErrorText = "The two teams must have different names.";
            return;
        }

        var blanks = team1.Players.Concat(team2.Players).Count(string.IsNullOrWhiteSpace);
        if (blanks > 0 && !_dialogs.Confirm($"{blanks} player name(s) are empty. Continue anyway?"))
            return;

        ErrorText = "";
        _match.ApplyTeamSetup(SelectedProfile?.Id, Format, team1, team2);

        // Temporary page until the real Controller UI exists (Milestones 5 to 7)
        var summary = $"{team1.Name} (Blue)  vs  {team2.Name} (Red)   |   {Format}\n\n" +
                      "The Controller screens are built in Milestones 5 to 7.";
        _navigator.NavigateTo(new PlaceholderViewModel("Controller", summary, _navigator));
    }
}