using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace MlbbOverlay.Services;

public partial class Navigator : ObservableObject
{
    private readonly IServiceProvider _provider;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    public Navigator(IServiceProvider provider)
    {
        _provider = provider;
    }
    public void Navigate<T>() where T : ObservableObject
        => CurrentViewModel = _provider.GetRequiredService<T>();

    public void NavigateTo(ObservableObject viewModel)
        => CurrentViewModel = viewModel;
}