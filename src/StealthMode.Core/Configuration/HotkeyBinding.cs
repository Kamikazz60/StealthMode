using StealthMode.Core.Native;

namespace StealthMode.Core.Configuration;

/// <summary>Raccourci global, sérialisé sous forme de texte (« Ctrl+Alt+F11 »).</summary>
public readonly record struct HotkeyBinding(uint Modifiers, uint VirtualKey)
{
    public static readonly HotkeyBinding DefaultActivate = new(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, 0x7A); // F11
    public static readonly HotkeyBinding DefaultRestore = new(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, 0x7B);  // F12

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((Modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((Modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Maj");
        if ((Modifiers & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(KeyName(VirtualKey));
        return string.Join("+", parts);
    }

    public static bool TryParse(string? text, out HotkeyBinding binding)
    {
        binding = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        uint mods = 0, vk = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= NativeMethods.MOD_CONTROL; break;
                case "alt": mods |= NativeMethods.MOD_ALT; break;
                case "maj": case "shift": mods |= NativeMethods.MOD_SHIFT; break;
                case "win": mods |= NativeMethods.MOD_WIN; break;
                default:
                    if (vk != 0 || !TryParseKey(raw, out vk)) return false;
                    break;
            }
        }
        if (vk == 0 || mods == 0) return false; // un raccourci global sans modificateur gênerait la frappe normale
        binding = new HotkeyBinding(mods, vk);
        return true;
    }

    static bool TryParseKey(string s, out uint vk)
    {
        vk = 0;
        s = s.ToUpperInvariant();
        if (s.Length >= 2 && s[0] == 'F' && int.TryParse(s.AsSpan(1), out var n) && n is >= 1 and <= 24) { vk = (uint)(0x70 + n - 1); return true; }
        if (s.Length == 1 && (char.IsAsciiLetterUpper(s[0]) || char.IsAsciiDigit(s[0]))) { vk = s[0]; return true; }
        return false;
    }

    static string KeyName(uint vk) =>
        vk is >= 0x70 and <= 0x87 ? $"F{vk - 0x70 + 1}" : ((char)vk).ToString();
}
