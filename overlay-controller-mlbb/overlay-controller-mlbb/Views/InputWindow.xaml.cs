using System.Windows;

namespace MlbbOverlay.Views;

/// <summary>A tiny "type one line of text" window.</summary>
public partial class InputWindow : Window
{
    public string? Result { get; private set; }

    public InputWindow(string title, string label, string initial = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = label;
        InputBox.Text = initial;
        Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}