using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Models;
using MlbbOverlay.Services;
using System.Data;
using System.Xml.Linq;

namespace MlbbOverlay.ViewModels;

public partial class HeroEditViewModel : ObservableObject
{
    private readonly HeroService _heroes;
    private readonly DialogService _dialogs;
    private readonly int _id;

    public string WindowTitle { get; }
    public IEnumerable<HeroRole> Roles { get; } = Enum.GetValues<HeroRole>();

    public ImageSlot Icon { get; } = new("Icon", "icon", "Small square image (used in pick/ban slots)");
    public ImageSlot Splash { get; } = new("Splash art", "splash", "Wide artwork (draft and loading style)");
    public ImageSlot FullBody { get; } = new("2D full body", "full", "Transparent PNG of the whole hero");

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private HeroRole _role = HeroRole.Unassigned;
    [ObservableProperty] private string _errorText = "";

    /// <summary>The window listens to this and closes itself (true = saved).</summary>
    public event Action<bool>? CloseRequested;

    public HeroEditViewModel(Hero? hero, HeroService heroes, DialogService dialogs)
    {
        _heroes = heroes;
        _dialogs = dialogs;

        if (hero == null)
        {
            WindowTitle = "Add hero";
        }
        else
        {
            WindowTitle = "Edit hero";
            _id = hero.Id;
            Name = hero.Name;
            Role = hero.Role;
            Icon.LoadExisting(hero.IconFile);
            Splash.LoadExisting(hero.SplashFile);
            FullBody.LoadExisting(hero.FullBodyFile);
        }
    }

    [RelayCommand]
    private void Browse(ImageSlot? slot)
    {
        if (slot == null) return;
        var file = _dialogs.PickImage();
        if (file != null) slot.SetNew(file);
    }

    [RelayCommand]
    private void Clear(ImageSlot? slot) => slot?.Clear();

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorText = "Please type a hero name.";
            return;
        }
        if (_heroes.NameExists(Name, _id))
        {
            ErrorText = "A hero with this name already exists.";
            return;
        }

        try
        {
            _heroes.Save(new Hero { Id = _id, Name = Name, Role = Role },
                         Icon.ToChange(), Splash.ToChange(), FullBody.ToChange());
            CloseRequested?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}