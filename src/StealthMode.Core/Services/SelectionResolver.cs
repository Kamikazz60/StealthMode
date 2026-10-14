using StealthMode.Core.Models;

namespace StealthMode.Core.Services;

/// <summary>Détermine les fenêtres effectivement masquées : sélection manuelle (fenêtre ou application) + règles du profil actif.</summary>
public static class SelectionResolver
{
    public static List<WindowInfo> Resolve(IReadOnlyList<WindowInfo> all, ISet<IntPtr> selectedHandles, SelectionMode mode,
        IEnumerable<HideRule>? rules = null)
    {
        var ruleList = rules?.ToList() ?? new List<HideRule>();
        HashSet<string>? apps = null;
        if (mode == SelectionMode.PerApplication)
            apps = all.Where(w => selectedHandles.Contains(w.Handle)).Select(w => w.AppKey).ToHashSet();

        return all.Where(w =>
            selectedHandles.Contains(w.Handle)
            || (apps is not null && apps.Contains(w.AppKey))
            || RuleMatcher.MatchesAny(ruleList, w)).ToList();
    }
}
