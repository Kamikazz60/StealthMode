using System.Windows.Interop;
using System.Windows.Media.Imaging;
using StealthMode.Core.Models;

namespace StealthMode.App.ViewModels;

public sealed class WindowItemViewModel : ObservableBase
{
    static readonly Dictionary<string, BitmapSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    readonly Action<WindowItemViewModel> _onSelectionChanged;
    bool _isSelected, _willBeHidden;

    public WindowItemViewModel(WindowInfo info, bool selected, Action<WindowItemViewModel> onSelectionChanged,
        System.Windows.Input.ICommand createRuleCommand, Action<WindowInfo, bool> addQuickRule)
    {
        Info = info;
        CreateRuleCommand = createRuleCommand;
        AddApplicationRuleCommand = new RelayCommand(() => addQuickRule(info, false));
        AddWindowRuleCommand = new RelayCommand(() => addQuickRule(info, true));
        _isSelected = selected;
        _onSelectionChanged = onSelectionChanged;
        Icon = LoadIcon(info.ExecutablePath);
    }

    public WindowInfo Info { get; }
    public System.Windows.Input.ICommand CreateRuleCommand { get; }
    /// <summary>Menu clic droit : ajoute directement au profil actif une règle sur l'application (toutes ses fenêtres).</summary>
    public System.Windows.Input.ICommand AddApplicationRuleCommand { get; }
    /// <summary>Menu clic droit : ajoute directement une règle sur cette fenêtre (application + titre).</summary>
    public System.Windows.Input.ICommand AddWindowRuleCommand { get; }

    string _profileName = "";
    /// <summary>Nom du profil actif, affiché dans le menu clic droit.</summary>
    public string ProfileName
    {
        get => _profileName;
        set { if (Set(ref _profileName, value)) { Raise(nameof(ApplicationRuleLabel)); Raise(nameof(WindowRuleLabel)); Raise(nameof(CustomRuleLabel)); } }
    }
    public string ApplicationRuleLabel => $"Toujours masquer {Info.ApplicationName} (profil « {_profileName} »)";
    public string WindowRuleLabel => $"Toujours masquer cette fenêtre : titre contenant « {ShortTitle} » (profil « {_profileName} »)";
    public string CustomRuleLabel => $"Créer une règle personnalisée… (profil « {_profileName} »)";
    string ShortTitle => Info.Title.Length > 40 ? Info.Title[..40] + "…" : Info.Title;
    public string Title => Info.Title;
    public string Subtitle => Info.IsMinimized ? $"{Info.ApplicationName} (réduite)" : Info.ApplicationName;
    public BitmapSource? Icon { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (Set(ref _isSelected, value)) _onSelectionChanged(this); }
    }

    /// <summary>Aperçu : cette fenêtre sera effectivement masquée à l'activation.</summary>
    public bool WillBeHidden
    {
        get => _willBeHidden;
        set => Set(ref _willBeHidden, value);
    }

    static BitmapSource? LoadIcon(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (IconCache.TryGetValue(path, out var cached)) return cached;
        BitmapSource? bmp = null;
        try
        {
            var small = new IntPtr[1];
            if (StealthMode.Core.Native.NativeMethods.ExtractIconEx(path, 0, null, small, 1) > 0 && small[0] != IntPtr.Zero)
            {
                try
                {
                    bmp = Imaging.CreateBitmapSourceFromHIcon(small[0], System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                }
                finally { StealthMode.Core.Native.NativeMethods.DestroyIcon(small[0]); }
            }
        }
        catch { /* icône indisponible : on affiche sans */ }
        IconCache[path] = bmp;
        return bmp;
    }
}
