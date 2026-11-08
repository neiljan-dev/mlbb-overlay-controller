using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;

namespace MlbbOverlay.ViewModels;

/// <summary>One pick or ban slot. It reads and writes its hero straight into the match's draft list.</summary>
public partial class DraftSlotViewModel : ObservableObject
{
    private readonly List<HeroSnapshot?> _store;
    private readonly Action<DraftSlotViewModel> _pick;
    private readonly Action<DraftSlotViewModel> _clear;

    public int Index { get; }
    public bool IsBan { get; }
    public string Ign { get; }          // player name (empty for bans)

    [ObservableProperty]
    private HeroSnapshot? _picked;

    public string? IconFile
    {
        get
        {
            var file = Picked?.IconFile;
            return string.IsNullOrEmpty(file) ? null : file;
        }
    }

    public string HeroName => Picked?.Name ?? "";

    public DraftSlotViewModel(List<HeroSnapshot?> store, int index, bool isBan, string ign,
                              Action<DraftSlotViewModel> pick, Action<DraftSlotViewModel> clear)
    {
        _store = store;
        Index = index;
        IsBan = isBan;
        Ign = ign;
        _pick = pick;
        _clear = clear;
        _picked = store[index];
    }

    partial void OnPickedChanged(HeroSnapshot? value)
    {
        OnPropertyChanged(nameof(IconFile));
        OnPropertyChanged(nameof(HeroName));
    }

    public void SetHero(HeroSnapshot? hero)
    {
        _store[Index] = hero;
        Picked = hero;
    }

    [RelayCommand]
    private void Pick() => _pick(this);

    [RelayCommand]
    private void Clear() => _clear(this);
}