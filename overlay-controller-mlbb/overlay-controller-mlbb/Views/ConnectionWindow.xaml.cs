using System.Windows;
using MlbbOverlay.ViewModels;

namespace MlbbOverlay.Views;

public partial class ConnectionWindow : Window
{
    private readonly ConnectionViewModel _viewModel;

    public ConnectionWindow(ConnectionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        PasswordInput.Password = viewModel.ObsPassword;
    }

    private void Password_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.ObsPassword = PasswordInput.Password;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}