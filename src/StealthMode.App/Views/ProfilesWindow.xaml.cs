using System.Windows;
using StealthMode.App.ViewModels;
using StealthMode.Core.Models;

namespace StealthMode.App.Views;

public partial class ProfilesWindow : Window
{
    /// <summary>« Terminer » : ferme uniquement cette fenêtre (les modifications sont déjà enregistrées).</summary>
    void Done_Click(object sender, RoutedEventArgs e) => Close();

    public ProfilesWindow(ProfilesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.AskText = (prompt, initial) => InputDialog.Ask(this, prompt, initial);
        vm.EditRule = rule => RuleEditorWindow.Edit(this, rule);
        // DereferenceLinks = false : on veut le raccourci (.lnk) lui-même, pas sa cible.
        vm.PickFiles = () =>
        {
            var d = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true, DereferenceLinks = false, CheckFileExists = true,
                Title = "Choisir les fichiers ou raccourcis à mettre de côté",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };
            return d.ShowDialog(this) == true ? d.FileNames : Array.Empty<string>();
        };
        vm.PickFolder = () =>
        {
            var d = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choisir le dossier à mettre de côté",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };
            return d.ShowDialog(this) == true ? d.FolderName : null;
        };
        vm.Confirm = text => MessageBox.Show(this, text, "Stealth Mode", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
