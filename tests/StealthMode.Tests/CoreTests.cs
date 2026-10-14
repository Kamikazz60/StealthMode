using StealthMode.Core.Configuration;
using StealthMode.Core.Models;
using StealthMode.Core.Services;

namespace StealthMode.Tests;

public class HotkeyBindingTests
{
    [Fact]
    public void DefaultsRoundTrip()
    {
        Assert.Equal("Ctrl+Alt+F11", HotkeyBinding.DefaultActivate.ToString());
        Assert.Equal("Ctrl+Alt+F12", HotkeyBinding.DefaultRestore.ToString());
        Assert.True(HotkeyBinding.TryParse("Ctrl+Alt+F11", out var b));
        Assert.Equal(HotkeyBinding.DefaultActivate, b);
    }

    [Theory]
    [InlineData("")]
    [InlineData("F11")]            // sans modificateur
    [InlineData("Ctrl+Alt")]       // sans touche
    [InlineData("Ctrl+Alt+Foo")]
    public void InvalidBindingsRejected(string text) => Assert.False(HotkeyBinding.TryParse(text, out _));
}

public class SelectionResolverTests
{
    static WindowInfo W(int h, string exe) =>
        new((IntPtr)h, $"t{h}", "cls", 1, "p", exe, "app", true, false, false);

    [Fact]
    public void PerWindow_OnlyCheckedWindows()
    {
        var all = new List<WindowInfo> { W(1, "chrome"), W(2, "chrome"), W(3, "code") };
        var r = SelectionResolver.Resolve(all, new HashSet<IntPtr> { (IntPtr)1 }, SelectionMode.PerWindow);
        Assert.Equal(new[] { (IntPtr)1 }, r.Select(w => w.Handle));
    }

    [Fact]
    public void PerApplication_AllWindowsOfCheckedApp()
    {
        var all = new List<WindowInfo> { W(1, "chrome"), W(2, "chrome"), W(3, "code") };
        var r = SelectionResolver.Resolve(all, new HashSet<IntPtr> { (IntPtr)1 }, SelectionMode.PerApplication);
        Assert.Equal(new[] { (IntPtr)1, (IntPtr)2 }, r.Select(w => w.Handle));
    }
}

[Collection("AppPaths")]
public class JsonFileTests
{
    [Fact]
    public void AtomicSaveThenLoad_AndInvalidFileFallsBackToDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stealth-test-" + Guid.NewGuid());
        AppPaths.Root = dir;
        try
        {
            new AppSettings { SelectByApplication = true }.Save();
            Assert.True(AppSettings.Load().SelectByApplication);
            Assert.False(File.Exists(AppPaths.SettingsFile + ".tmp"));

            File.WriteAllText(AppPaths.SettingsFile, "{ corrompu");
            Assert.False(AppSettings.Load().SelectByApplication);
        }
        finally { Directory.Delete(dir, true); }
    }
}
