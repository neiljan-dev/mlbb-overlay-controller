using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

public partial class PlaceholderViewModel : ObservableObject
{
    private readonly Navigator _navigator;

    public string Title { get; }
    public string Message { get; }

    public PlaceholderViewModel(string title, string message, Navigator navigator)
    {
        Title = title;
        Message = message;
        _navigator = navigator;
    }

    [RelayCommand]
    private void Back() => _navigator.Navigate<MainMenuViewModel>();
}