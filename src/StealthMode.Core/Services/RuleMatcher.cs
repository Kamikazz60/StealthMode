using StealthMode.Core.Models;

namespace StealthMode.Core.Services;

public static class RuleMatcher
{
    public static bool Matches(HideRule rule, WindowInfo w)
    {
        if (!rule.HasCriteria) return false; // une règle vide ne doit jamais tout masquer

        if (!string.IsNullOrWhiteSpace(rule.ProcessName) &&
            !string.Equals(Path.GetFileNameWithoutExtension(rule.ProcessName.Trim()), w.ProcessName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(rule.ExecutablePath) &&
            !string.Equals(rule.ExecutablePath.Trim(), w.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(rule.TitleContains) &&
            w.Title.IndexOf(rule.TitleContains.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return true;
    }

    public static bool MatchesAny(IEnumerable<HideRule> rules, WindowInfo w) => rules.Any(r => Matches(r, w));
}
