using System.Windows;

namespace MlbbOverlay.Views;

/// <summary>Asks for one or two whole numbers (minutes, scores).</summary>
public partial class NumberInputWindow : Window
{
    private readonly int _min;

    public int Value1 { get; private set; }
    public int Value2 { get; private set; }

    public NumberInputWindow(string title, string label1, int initial1, string? label2, int initial2, int min)
    {
        InitializeComponent();
        Title = title;
        _min = min;

        Label1Text.Text = label1;
        Box1.Text = initial1.ToString();

        if (label2 == null)
        {
            SecondPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            Label2Text.Text = label2;
            Box2.Text = initial2.ToString();
        }

        Loaded += (_, _) => { Box1.Focus(); Box1.SelectAll(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var message = $"Please type a whole number, {_min} or higher.";

        if (!int.TryParse(Box1.Text.Trim(), out var first) || first < _min)
        {
            ErrorText.Text = message;
            return;
        }

        var second = 0;
        if (SecondPanel.Visibility == Visibility.Visible &&
            (!int.TryParse(Box2.Text.Trim(), out second) || second < _min))
        {
            ErrorText.Text = message;
            return;
        }

        Value1 = first;
        Value2 = second;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}