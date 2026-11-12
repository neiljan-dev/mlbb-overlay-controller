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
    private void OpenController() => _navigator.Navigate<TeamSetupViewModel>();

    [RelayCommand]
    private void OpenDatabase() => _navigator.Navigate<DatabaseViewModel>();

    [RelayCommand]
    private void OpenDesign() => _navigator.Navigate<DesignListViewModel>();
}