using System.Windows;
using MlbbOverlay.ViewModels;

namespace MlbbOverlay;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}