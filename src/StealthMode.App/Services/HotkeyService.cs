using System.ComponentModel;
using System.Windows.Interop;
using StealthMode.Core.Configuration;
using StealthMode.Core.Native;

namespace StealthMode.App.Services;

/// <summary>Raccourcis globaux via RegisterHotKey, sur une fenêtre de messages cachée (aucun hook clavier global).</summary>
public sealed class HotkeyService : IDisposable
{
    const int ActivateId = 0x5301, RestoreId = 0x5302;

    readonly HwndSource _source;

    public event Action? ActivatePressed;
    public event Action? RestorePressed;

    public bool ActivateRegistered { get; private set; }
    public bool RestoreRegistered { get; private set; }

    public HotkeyService()
    {
        var p = new HwndSourceParameters("StealthModeHotkeys") { ParentWindow = new IntPtr(-3) /* HWND_MESSAGE */ };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    /// <summary>Enregistre les raccourcis ; retourne un message d'erreur par raccourci indisponible.</summary>
    /// <param name="restore">Raccourci de restauration distinct ; null = un seul raccourci qui bascule (il sert aussi à restaurer).</param>
    public List<string> Register(HotkeyBinding activate, HotkeyBinding? restore)
    {
        var errors = new List<string>();
        NativeMethods.UnregisterHotKey(_source.Handle, RestoreId);
        if (restore is null)
        {
            ActivateRegistered = TryRegister(ActivateId, activate, "(activation / restauration)", errors);
            RestoreRegistered = ActivateRegistered;
            return errors;
        }
        ActivateRegistered = TryRegister(ActivateId, activate, "d'activation", errors);
        RestoreRegistered = TryRegister(RestoreId, restore.Value, "de restauration", errors);
        return errors;
    }

    /// <summary>Libère les deux raccourcis (ex. pendant leur saisie dans les paramètres, pour que la combinaison atteigne le champ).</summary>
    public void Unregister()
    {
        NativeMethods.UnregisterHotKey(_source.Handle, ActivateId);
        NativeMethods.UnregisterHotKey(_source.Handle, RestoreId);
        ActivateRegistered = RestoreRegistered = false;
    }

    bool TryRegister(int id, HotkeyBinding b, string label, List<string> errors)
    {
        NativeMethods.UnregisterHotKey(_source.Handle, id);
        if (NativeMethods.RegisterHotKey(_source.Handle, id, b.Modifiers | NativeMethods.MOD_NOREPEAT, b.VirtualKey))
        {
            Logger.Info($"Raccourci {label} {b} enregistré");
            return true;
        }

        int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        var reason = err == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
            ? "déjà utilisé par un autre logiciel"
            : new Win32Exception(err).Message;
        var msg = $"Raccourci {label} {b} indisponible : {reason}";
        errors.Add(msg);
        Logger.Error(msg);
        return false;
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            Logger.Info($"Raccourci reçu : {(id == ActivateId ? "activation" : "restauration")}");
            if (id == ActivateId) { ActivatePressed?.Invoke(); handled = true; }
            else if (id == RestoreId) { RestorePressed?.Invoke(); handled = true; }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        NativeMethods.UnregisterHotKey(_source.Handle, ActivateId);
        NativeMethods.UnregisterHotKey(_source.Handle, RestoreId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
