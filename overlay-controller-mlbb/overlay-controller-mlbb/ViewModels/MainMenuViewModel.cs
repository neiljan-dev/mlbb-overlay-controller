using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

public partial class MainMenuViewModel : ObservableObject
{
    private readonly Navigator _navigator;

    public MainMenuViewModel(Navigator navigator)
    {
        _navigator = navigator;
    }
    [RelayCommand]
    private void OpenController() =>
        _navigator.NavigateTo(new PlaceholderViewModel("Controller", "Coming in Milestones 3 to 7", _navigator));

    [RelayCommand]
    private void OpenDatabase() =>
        _navigator.NavigateTo(new PlaceholderViewModel("Database", "Coming in Milestone 2", _navigator));

    [RelayCommand]
    private void OpenDesign() =>
        _navigator.NavigateTo(new PlaceholderViewModel("Design x Animation", "Coming in Milestone 9", _navigator));
}