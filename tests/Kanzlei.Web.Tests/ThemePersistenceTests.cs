using System.Net;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kanzlei.Web.Tests;

public sealed class ThemePersistenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kanzlei-theme-" + Guid.NewGuid().ToString("N"));
    private string File_ => Path.Combine(_dir, "sub", "theme.css");

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private static Dictionary<string, string?> Values(string primary)
    {
        var v = ThemeService.Defaults().ToDictionary(p => p.Key, p => (string?)p.Value);
        v["primary"] = primary;
        return v;
    }

    [Fact]
    public void MissingFile_UsesDefaults_AndDoesNotCreateFile()
    {
        var svc = new ThemeService(File_);
        Assert.Equal(ThemeService.Defaults(), svc.Current);
        Assert.False(File.Exists(File_));
    }

    [Fact]
    public void Apply_CreatesDirectory_WritesCompleteCss_AndSurvivesRestart()
    {
        var svc = new ThemeService(File_);
        Assert.True(svc.TryApply(Values("#AA0000"), out _));
        var text = File.ReadAllText(File_);
        Assert.Contains("--app-primary: #aa0000;", text);
        Assert.Contains("--app-primary-contrast: #ffffff;", text);
        Assert.All(ThemeService.TokenList, t => Assert.Contains(t.CssVariable + ":", text));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(File_)!, "*.tmp"));

        var restarted = new ThemeService(File_);
        Assert.Equal("#aa0000", restarted.Current["primary"]);
        Assert.Equal(svc.BuildCss(), restarted.BuildCss());
    }

    [Fact]
    public void Reset_PersistsDefaults()
    {
        var svc = new ThemeService(File_);
        svc.TryApply(Values("#aa0000"), out _);
        Assert.True(svc.Reset());
        Assert.Equal(ThemeService.Defaults(), new ThemeService(File_).Current);
        Assert.Contains("--app-primary: #1f4e79;", File.ReadAllText(File_));
    }

    [Fact]
    public void InvalidApply_DoesNotTouchFile()
    {
        var svc = new ThemeService(File_);
        svc.TryApply(Values("#aa0000"), out _);
        var before = File.ReadAllText(File_);
        Assert.False(svc.TryApply(Values("red"), out _));
        Assert.Equal(before, File.ReadAllText(File_));
        Assert.Equal("#aa0000", svc.Current["primary"]);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData(":root {\n  --app-primary: #aa0000;\n}\n")]
    [InlineData(":root {\n  --app-evil: #aa0000;\n}\n")]
    [InlineData("body { display:none }")]
    public void CorruptFile_FallsBackToDefaults(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, content);
        Assert.Equal(ThemeService.Defaults(), new ThemeService(File_).Current);
    }

    [Fact]
    public void UntrustedDeclarations_AreRejected()
    {
        var good = new ThemeService(File_);
        good.TryApply(Values("#aa0000"), out _);
        var valid = File.ReadAllText(File_);
        foreach (var bad in new[]
        {
            valid.Replace("--app-primary: #aa0000;", "--app-primary: url(http://x/y);"),
            valid.Replace("}\n", "  background: url(x);\n}\n"),
            valid + "body { display:none }\n",
            valid.Replace("--app-primary: #aa0000;", "--app-primary: #aa0000; } body { display:none } :root {"),
            valid.Replace("--app-text: #212529;", "--app-primary: #123456;"),
            "/* x */ @import url(x);\n" + valid,
        })
        {
            File.WriteAllText(File_, bad);
            Assert.Equal(ThemeService.Defaults(), new ThemeService(File_).Current);
        }
    }

    [Fact]
    public void WriteFailure_KeepsPreviousTheme()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        var svc = new ThemeService(Path.Combine(blocker, "theme.css"));
        Assert.False(svc.TryApply(Values("#aa0000"), out var errors));
        Assert.Contains(ThemeService.PersistErrorKey, errors.Keys);
        Assert.Equal(ThemeService.Defaults(), svc.Current);
    }

    [Fact]
    public async Task ConfiguredPath_IsUsedByApp_AndReloadedOnRestart()
    {
        var path = File_;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var seed = new ThemeService(path);
        seed.TryApply(Values("#0a0b0c"), out _);

        using var factory = new TestAppFactory().WithWebHostBuilder(b =>
            b.UseSetting("THEME_CSS_FILE", path));
        var client = factory.CreateClient();
        var response = await client.GetAsync("/theme.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("--app-primary: #0a0b0c;", await response.Content.ReadAsStringAsync());
    }
}
