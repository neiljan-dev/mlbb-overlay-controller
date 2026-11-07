using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

public partial class DatabaseViewModel : ObservableObject
{
    private readonly Navigator _navigator;
    private readonly HeroService _heroes;
    private readonly BulkImportService _import;
    private readonly DialogService _dialogs;

    public ObservableCollection<Hero> Heroes { get; } = new();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _statusText = "";

    public DatabaseViewModel(Navigator navigator, HeroService heroes,
                             BulkImportService import, DialogService dialogs)
    {
        _navigator = navigator;
        _heroes = heroes;
        _import = import;
        _dialogs = dialogs;
        Load();
    }

    partial void OnSearchTextChanged(string value) => Load();

    private void Load()
    {
        Heroes.Clear();
        foreach (var hero in _heroes.GetAll(SearchText)) Heroes.Add(hero);
        StatusText = $"Showing {Heroes.Count} of {_heroes.Count()} heroes";
    }

    [RelayCommand]
    private void Back() => _navigator.Navigate<MainMenuViewModel>();

    [RelayCommand]
    private void AddHero()
    {
        var editor = new HeroEditViewModel(null, _heroes, _dialogs);
        if (_dialogs.ShowHeroEditor(editor)) Load();
    }

    [RelayCommand]
    private void EditHero(Hero? hero)
    {
        if (hero == null) return;
        var editor = new HeroEditViewModel(hero, _heroes, _dialogs);
        if (_dialogs.ShowHeroEditor(editor)) Load();
    }

    [RelayCommand]
    private void DeleteHero(Hero? hero)
    {
        if (hero == null) return;
        if (!_dialogs.Confirm($"Delete \"{hero.Name}\" and its images?")) return;

        try
        {
            _heroes.Delete(hero.Id);
            Load();
        }
        catch (Exception ex)
        {
            _dialogs.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void ImportFolder()
    {
        var folder = _dialogs.PickFolder();
        if (folder == null) return;

        try
        {
            var result = _import.Import(folder);
            Load();

            var message = $"Created: {result.Created}\nUpdated: {result.Updated}\nSkipped files: {result.Skipped.Count}";
            if (result.Skipped.Count > 0)
            {
                message += "\n\nSkipped (name must look like Layla_icon.png):\n"
                           + string.Join("\n", result.Skipped.Take(15));
                if (result.Skipped.Count > 15) message += "\n...";
            }
            _dialogs.Info(message, "Import finished");
        }
        catch (Exception ex)
        {
            _dialogs.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void TestPicker()
    {
        var picked = _dialogs.ShowHeroPicker(new HeroPickerViewModel(_heroes));
        if (picked != null) _dialogs.Info($"You picked: {picked.Name} ({picked.Role})", "Picker test");
    }
}