using System.Windows;
using MlbbOverlay.ViewModels;

namespace MlbbOverlay.Views;

public partial class HeroPickerWindow : Window
{
    public HeroPickerWindow(HeroPickerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += picked => DialogResult = picked;
    }
}