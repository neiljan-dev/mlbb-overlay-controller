using System.Windows;
using MlbbOverlay.ViewModels;

namespace MlbbOverlay.Views;

public partial class PlayerChoiceWindow : Window
{
    public PlayerChoiceWindow(PlayerChoiceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += chosen => DialogResult = chosen;
    }
}