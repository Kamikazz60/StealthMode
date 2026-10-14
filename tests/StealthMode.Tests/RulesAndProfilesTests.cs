using StealthMode.Core.Configuration;
using StealthMode.Core.Models;
using StealthMode.Core.Services;

namespace StealthMode.Tests;

public class RuleMatcherTests
{
    static WindowInfo W(int h, string title, string proc = "Code", string? exe = @"C:\Apps\Code.exe") =>
        new((IntPtr)h, title, "cls", 1, proc, exe, "app", true, false, false);

    [Fact]
    public void EmptyRuleNeverMatches() =>
        Assert.False(RuleMatcher.Matches(new HideRule(), W(1, "x")));

    [Fact]
    public void ProcessRule_MatchesAllWindowsOfProcess_CaseInsensitive()
    {
        var rule = new HideRule { ProcessName = "code.exe" };
        Assert.True(RuleMatcher.Matches(rule, W(1, "a")));
        Assert.True(RuleMatcher.Matches(rule, W(2, "b")));
        Assert.False(RuleMatcher.Matches(rule, W(3, "c", proc: "chrome", exe: @"C:\Chrome\chrome.exe")));
    }

    [Fact]
    public void ProcessAndTitle_BothMustMatch()
    {
        var rule = new HideRule { ProcessName = "chrome", TitleContains = "gmail" };
        Assert.True(RuleMatcher.Matches(rule, W(1, "Boîte - GMAIL perso", "chrome", @"C:\c\chrome.exe")));
        Assert.False(RuleMatcher.Matches(rule, W(2, "YouTube", "chrome", @"C:\c\chrome.exe")));
        Assert.False(RuleMatcher.Matches(rule, W(3, "Gmail", "firefox", @"C:\f\firefox.exe")));
    }

    [Fact]
    public void ExecutablePathRule()
    {
        var rule = new HideRule { ExecutablePath = @"c:\apps\code.exe" };
        Assert.True(RuleMatcher.Matches(rule, W(1, "a")));
        Assert.False(RuleMatcher.Matches(rule, W(2, "a", exe: null)));
    }

    [Fact]
    public void Resolver_UnionsManualSelectionAndRules_WithoutDuplicates()
    {
        var all = new List<WindowInfo> { W(1, "a"), W(2, "b", "chrome", @"C:\c\chrome.exe"), W(3, "c", "chrome", @"C:\c\chrome.exe") };
        var rules = new[] { new HideRule { ProcessName = "Code" } };
        var r = SelectionResolver.Resolve(all, new HashSet<IntPtr> { (IntPtr)1, (IntPtr)2 }, SelectionMode.PerWindow, rules);
        Assert.Equal(new[] { (IntPtr)1, (IntPtr)2 }, r.Select(w => w.Handle));
    }
}

[Collection("AppPaths")]
public class ProfilesDataTests
{
    [Fact]
    public void Normalize_CreatesDefaultProfile_AndFixesInvalidActive()
    {
        var d = new ProfilesData { ActiveProfileId = Guid.NewGuid() }.Normalize();
        Assert.Single(d.Profiles);
        Assert.Equal(d.Profiles[0].Id, d.ActiveProfileId);
    }

    [Fact]
    public void SaveLoad_RoundTrip_AndCorruptedFileFallsBack()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stealth-test-" + Guid.NewGuid());
        AppPaths.Root = dir;
        try
        {
            var d = ProfilesData.Load();
            var p = new Profile { Name = "Travail", Rules = { new HideRule { Name = "VS Code", ProcessName = "Code" } } };
            d.Profiles.Add(p);
            d.ActiveProfileId = p.Id;
            d.Save();

            var loaded = ProfilesData.Load();
            Assert.Equal("Travail", loaded.Active.Name);
            Assert.Equal("Code", loaded.Active.Rules.Single().ProcessName);
            Assert.DoesNotContain("Summary", File.ReadAllText(AppPaths.ProfilesFile)); // propriété d'affichage non sérialisée

            File.WriteAllText(AppPaths.ProfilesFile, "n'importe quoi");
            Assert.Single(ProfilesData.Load().Profiles);
        }
        finally { Directory.Delete(dir, true); }
    }
}
