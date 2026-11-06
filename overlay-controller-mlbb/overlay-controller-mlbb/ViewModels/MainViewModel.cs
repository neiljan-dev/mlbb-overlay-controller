using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;
public class MainViewModel
{
    public Navigator Navigator { get; }

    public MainViewModel(Navigator navigator)
    {
        Navigator = navigator;
        Navigator.Navigate<MainMenuViewModel>();   // first screen
    }
}