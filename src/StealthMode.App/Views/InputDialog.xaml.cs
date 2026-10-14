using System.Windows;

namespace StealthMode.App.Views;

public partial class InputDialog : Window
{
    public string? Value { get; private set; }

    public InputDialog(string prompt, string initial)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        Input.Text = initial;
        Loaded += (_, _) => { Input.Focus(); Input.SelectAll(); };
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Input.Text)) return;
        Value = Input.Text.Trim();
        DialogResult = true;
    }

    /// <summary>Retourne le texte saisi, ou null si annulé.</summary>
    public static string? Ask(Window? owner, string prompt, string initial = "")
    {
        var d = new InputDialog(prompt, initial) { Owner = owner };
        return d.ShowDialog() == true ? d.Value : null;
    }
}
