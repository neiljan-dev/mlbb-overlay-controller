using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

/// <summary>The "choose a hero" window with a search bar. Reused in Hero Selection (Milestone 5).</summary>
public partial class HeroPickerViewModel : ObservableObject
{
    private readonly HeroService _heroes;

    public ObservableCollection<Hero> Heroes { get; } = new();
    public Hero? Result { get; private set; }

    [ObservableProperty] private string _searchText = "";

    public event Action<bool>? CloseRequested;

    public HeroPickerViewModel(HeroService heroes)
    {
        _heroes = heroes;
        Load();
    }

    partial void OnSearchTextChanged(string value) => Load();

    private void Load()
    {
        Heroes.Clear();
        foreach (var hero in _heroes.GetAll(SearchText)) Heroes.Add(hero);
    }

    [RelayCommand]
    private void Pick(Hero? hero)
    {
        if (hero == null) return;
        Result = hero;
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}