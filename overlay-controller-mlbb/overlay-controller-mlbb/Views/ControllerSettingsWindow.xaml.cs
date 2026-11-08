using System.Windows;
using System.Windows.Input;
using MlbbOverlay.Services;
using MlbbOverlay.ViewModels;

namespace MlbbOverlay.Views;

public partial class ControllerSettingsWindow : Window
{
    private readonly ControllerSettingsViewModel _viewModel;

    public ControllerSettingsWindow(ControllerSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    // While a hotkey is waiting for a new key, this window takes the key press
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (HotkeyService.IsModifierKey(key)) return;

        if (_viewModel.HandleKey(key, Keyboard.Modifiers)) e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}