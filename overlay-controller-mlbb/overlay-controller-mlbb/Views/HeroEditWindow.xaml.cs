using System.Windows;
using MlbbOverlay.ViewModels;

namespace MlbbOverlay.Views;

public partial class HeroEditWindow : Window
{
    public HeroEditWindow(HeroEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += saved => DialogResult = saved;   // closes the window
    }
}