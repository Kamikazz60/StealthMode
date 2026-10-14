using System.Windows;
using StealthMode.App.ViewModels;

namespace StealthMode.App.Views;

public partial class MainWindow : Window
{
    DateTime _helpClosedAt = DateTime.MinValue;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.Refresh();
        HelpPopup.Closed += (_, _) => _helpClosedAt = DateTime.UtcNow;
    }

    /// <summary>Ouvre la bulle d'aide. Un clic sur « ? » alors qu'elle est ouverte la ferme (sans la rouvrir aussitôt).</summary>
    void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        if ((DateTime.UtcNow - _helpClosedAt).TotalMilliseconds < 250) return;
        HelpPopup.IsOpen = true;
    }
}
