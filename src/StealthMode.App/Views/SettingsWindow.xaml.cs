using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StealthMode.App.ViewModels;
using StealthMode.Core.Configuration;
using StealthMode.Core.Native;

namespace StealthMode.App.Views;

public partial class SettingsWindow : Window
{
    readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    /// <summary>Capture d'une combinaison de touches dans le champ de raccourci.</summary>
    void Hotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.Tab or Key.Escape) return;

        uint mods = 0;
        var m = Keyboard.Modifiers;
        if ((m & ModifierKeys.Control) != 0) mods |= NativeMethods.MOD_CONTROL;
        if ((m & ModifierKeys.Alt) != 0) mods |= NativeMethods.MOD_ALT;
        if ((m & ModifierKeys.Shift) != 0) mods |= NativeMethods.MOD_SHIFT;
        if ((m & ModifierKeys.Windows) != 0) mods |= NativeMethods.MOD_WIN;

        var binding = new HotkeyBinding(mods, (uint)KeyInterop.VirtualKeyFromKey(key));
        // On n'accepte que ce que le format sait relire (F1-F24, lettres, chiffres, avec modificateur).
        if (!HotkeyBinding.TryParse(binding.ToString(), out binding))
        {
            _vm.Message = "Combinaison non prise en charge : modificateur(s) + F1-F24, lettre ou chiffre.";
            return;
        }
        _vm.Message = "";
        if ((sender as TextBox)?.Tag as string == "activate") _vm.ActivateText = binding.ToString();
        else _vm.RestoreText = binding.ToString();
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.SaveCommand.Execute(null);
        if (_vm.Succeeded) DialogResult = true;
    }
}
