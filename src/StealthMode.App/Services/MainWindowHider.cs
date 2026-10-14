using System.Windows;
using System.Windows.Interop;
using static StealthMode.Core.Native.NativeMethods;

namespace StealthMode.App.Services;

/// <summary>
/// Masque et réaffiche la fenêtre principale comme n'importe quelle autre fenêtre masquée :
/// l'état d'avant (réduite, normale, agrandie, ou déjà cachée dans la zone de notification) est conservé
/// et la fenêtre ne prend jamais le focus ni le premier plan en revenant.
/// </summary>
public sealed class MainWindowHider
{
    readonly Window _window;
    bool _hidden, _wasMinimized;

    public MainWindowHider(Window window) => _window = window;

    IntPtr Handle => new WindowInteropHelper(_window).EnsureHandle();

    /// <summary>Masque la fenêtre si elle est affichée (même réduite). Si elle était déjà cachée, elle le reste.</summary>
    public void Hide()
    {
        var h = Handle;
        _hidden = false;
        if (!IsWindowVisible(h)) return;
        _wasMinimized = IsIconic(h);
        ShowWindow(h, SW_HIDE);
        _hidden = true;
    }

    /// <summary>
    /// Réaffiche la fenêtre dans son état d'origine, sans l'activer. Si <paramref name="show"/> est faux elle reste cachée
    /// (accessible par l'icône de notification). Retourne vrai si elle est de nouveau affichée.
    /// </summary>
    public bool Restore(bool show)
    {
        if (!_hidden) return false;
        _hidden = false;
        if (!show) return false;
        // Réduite avant : elle revient réduite ; sinon elle revient telle qu'elle était (normale ou agrandie), à sa place dans la pile.
        ShowWindow(Handle, _wasMinimized ? SW_SHOWMINNOACTIVE : SW_SHOWNA);
        return true;
    }

    /// <summary>Ouverture explicite par l'utilisateur (icône de notification) : là, la fenêtre passe au premier plan.</summary>
    public void ShowAndActivate()
    {
        _hidden = false;
        _window.Show();
        var h = Handle;
        if (!IsWindowVisible(h)) ShowWindow(h, SW_SHOW_DEFAULT);
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    const int SW_SHOW_DEFAULT = 5;
}
