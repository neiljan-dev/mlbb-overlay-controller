using System.Windows;
using MlbbOverlay.Models;

namespace MlbbOverlay.Views;

public partial class TeamChoiceWindow : Window
{
    /// <summary>1 or 2 (Team 1 or Team 2).</summary>
    public int Result { get; private set; }

    public TeamChoiceWindow(TeamChoiceModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    private void Team_Click(object sender, RoutedEventArgs e)
    {
        Result = Convert.ToInt32(((FrameworkElement)sender).Tag);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}