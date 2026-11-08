using System.Windows;
using Microsoft.Win32;
using MlbbOverlay.Models;
using MlbbOverlay.ViewModels;
using MlbbOverlay.Views;

namespace MlbbOverlay.Services;

/// <summary>Opens file pickers, message boxes and dialog windows, so ViewModels contain no UI code.</summary>
public class DialogService
{
    private static Window Owner => Application.Current.MainWindow!;

    public string? PickImage()
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.webp|All files|*.*" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog();
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public bool Confirm(string message, string title = "Please confirm") =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;

    public void Info(string message, string title = "Info") =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string message, string title = "Something went wrong") =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    /// <summary>Opens the add/edit window. Returns true if the hero was saved.</summary>
    public bool ShowHeroEditor(HeroEditViewModel viewModel) =>
        new HeroEditWindow(viewModel) { Owner = Owner }.ShowDialog() == true;

    /// <summary>Opens the hero picker. Returns the chosen hero, or null if cancelled.</summary>
    public Hero? ShowHeroPicker(HeroPickerViewModel viewModel)
    {
        var picked = new HeroPickerWindow(viewModel) { Owner = Owner }.ShowDialog() == true;
        return picked ? viewModel.Result : null;
    }

    /// <summary>Asks for one line of text. Returns null if cancelled.</summary>
    public string? Prompt(string title, string label, string initial = "")
    {
        var window = new InputWindow(title, label, initial) { Owner = Owner };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public void ShowConnection(ConnectionViewModel viewModel) =>
        new ConnectionWindow(viewModel) { Owner = Owner }.ShowDialog();

    public void CopyToClipboard(string text)
    {
        try { Clipboard.SetText(text); }
        catch { /* clipboard busy: ignore */ }
    }

    public PlayerChoice? ShowPlayerChoice(PlayerChoiceViewModel viewModel)
    {
        var chosen = new PlayerChoiceWindow(viewModel) { Owner = Owner }.ShowDialog() == true;
        return chosen ? viewModel.Result : null;
    }

    /// <summary>Returns 1 or 2 (the chosen team), or null if cancelled.</summary>
    public int? ShowTeamChoice(TeamChoiceModel model)
    {
        var window = new TeamChoiceWindow(model) { Owner = Owner };
        return window.ShowDialog() == true ? window.Result : null;
    }

    /// <summary>One or two whole numbers (label2 = null for one). Returns null if cancelled.</summary>
    public (int First, int Second)? ShowNumbers(string title, string label1, int initial1 = 0,
                                                string? label2 = null, int initial2 = 0, int min = 0)
    {
        var window = new NumberInputWindow(title, label1, initial1, label2, initial2, min) { Owner = Owner };
        return window.ShowDialog() == true ? (window.Value1, window.Value2) : null;
    }

    public void ShowControllerSettings(ControllerSettingsViewModel viewModel) =>
        new ControllerSettingsWindow(viewModel) { Owner = Owner }.ShowDialog();
}