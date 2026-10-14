using System.Windows;
using StealthMode.Core.Models;

namespace StealthMode.App.Views;

public partial class RuleEditorWindow : Window
{
    readonly HideRule _rule;

    /// <summary>Résultat après validation (copie éditée de la règle d'origine).</summary>
    public HideRule? Result { get; private set; }

    public RuleEditorWindow(HideRule rule)
    {
        InitializeComponent();
        _rule = rule;
        NameBox.Text = rule.Name;
        ProcessBox.Text = rule.ProcessName;
        PathBox.Text = rule.ExecutablePath;
        TitleBox.Text = rule.TitleContains;
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        string? N(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var r = new HideRule
        {
            Id = _rule.Id,
            Name = NameBox.Text.Trim(),
            ProcessName = N(ProcessBox.Text),
            ExecutablePath = N(PathBox.Text),
            TitleContains = N(TitleBox.Text)
        };
        if (!r.HasCriteria) { ErrorText.Text = "Renseignez au moins un critère (processus, chemin ou titre)."; return; }
        if (r.Name.Length == 0) r.Name = r.Describe();
        Result = r;
        DialogResult = true;
    }

    /// <summary>Ouvre l'éditeur ; retourne la règle validée, ou null si annulé.</summary>
    public static HideRule? Edit(Window? owner, HideRule rule)
    {
        var w = new RuleEditorWindow(rule) { Owner = owner };
        return w.ShowDialog() == true ? w.Result : null;
    }
}
