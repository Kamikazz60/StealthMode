using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace StealthMode.App.Services;

/// <summary>Icône de la zone de notification : filet de sécurité pour restaurer sans raccourci clavier.</summary>
public sealed class TrayService : IDisposable
{
    readonly WinForms.NotifyIcon _icon;

    public TrayService(Action open, Action activate, Action restore, Action settings, Action exit)
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Ouvrir Stealth Mode", null, (_, _) => open());
        menu.Items.Add("Activer le mode secret", null, (_, _) => activate());
        menu.Items.Add("Restaurer les fenêtres", null, (_, _) => restore());
        menu.Items.Add("Paramètres", null, (_, _) => settings());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => exit());

        _icon = new WinForms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Stealth Mode",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => open();
    }

    /// <summary>Icône de l'application (ressource intégrée) à la taille de la zone de notification ; icône système en repli.</summary>
    static Drawing.Icon LoadIcon()
    {
        try
        {
            var res = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/StealthMode.ico"));
            if (res is not null) using (res.Stream) return new Drawing.Icon(res.Stream, WinForms.SystemInformation.SmallIconSize);
        }
        catch { /* repli ci-dessous */ }
        return Drawing.SystemIcons.Shield;
    }

    public void SetVisible(bool visible) => _icon.Visible = visible;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
