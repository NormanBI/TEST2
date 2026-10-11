using System.Net;
using System.Text.RegularExpressions;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kanzlei.Web.Tests;

public class ThemeServiceTests
{
    private static Dictionary<string, string?> Defaults() =>
        ThemeService.Defaults().ToDictionary(p => p.Key, p => (string?)p.Value);

    [Theory]
    [InlineData("#1f4e79", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("#fff", false)]
    [InlineData("red", false)]
    [InlineData("#12345g", false)]
    [InlineData("#1f4e79;} body{display:none", false)]
    [InlineData("url(x)", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidHex_IsStrict(string? value, bool expected) =>
        Assert.Equal(expected, ThemeService.IsValidHex(value));

    [Fact]
    public void Apply_ValidValues_ChangesCurrentAndCss()
    {
        var svc = new ThemeService();
        var values = Defaults();
        values["primary"] = "#AA0000";
        Assert.True(svc.TryApply(values, out var errors));
        Assert.Empty(errors);
        Assert.Equal("#aa0000", svc.Current["primary"]);
        Assert.Contains("--app-primary: #aa0000;", svc.BuildCss());
    }

    [Fact]
    public void Apply_InvalidValue_IsRejectedAndKeepsTheme()
    {
        var svc = new ThemeService();
        var values = Defaults();
        values["link"] = "javascript:alert(1)";
        values["primary"] = "#aa0000";
        Assert.False(svc.TryApply(values, out var errors));
        Assert.Contains("link", errors.Keys);
        Assert.Equal(ThemeService.Defaults()["primary"], svc.Current["primary"]);
    }

    [Fact]
    public void Apply_MissingToken_IsRejected()
    {
        var svc = new ThemeService();
        var values = Defaults();
        values.Remove("text");
        Assert.False(svc.TryApply(values, out _));
    }

    [Fact]
    public void Reset_RestoresDefaults()
    {
        var svc = new ThemeService();
        var values = Defaults();
        values["background"] = "#000000";
        svc.TryApply(values, out _);
        svc.Reset();
        Assert.Equal(ThemeService.Defaults(), svc.Current);
    }

    [Fact]
    public void Defaults_CoverRequiredTokens_AndTextIsReadable()
    {
        Assert.True(ThemeService.TokenList.Count >= 13);
        var d = ThemeService.Defaults();
        Assert.All(d.Values, v => Assert.True(ThemeService.IsValidHex(v)));
        Assert.True(ThemeService.ContrastRatio(d["text"], d["background"]) >= 4.5);
        Assert.True(ThemeService.ContrastRatio(d["muted"], d["surface"]) >= 4.5);
    }
}

public class ThemeEndpointTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public ThemeEndpointTests(TestAppFactory factory) => _factory = factory;

    private HttpClient NewClient(TestAppFactory? f = null) => (f ?? _factory).CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    private static async Task<string> TokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        return Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    private async Task<HttpClient> LoginAsync(string user, string password, TestAppFactory? f = null)
    {
        var client = NewClient(f);
        var token = await TokenAsync(client, "/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserName"] = user,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static Dictionary<string, string> Form(string token, string primary)
    {
        var form = ThemeService.Defaults().ToDictionary(p => $"values[{p.Key}]", p => p.Value);
        form["values[primary]"] = primary;
        form["__RequestVerificationToken"] = token;
        return form;
    }

    [Fact]
    public async Task Layout_Anonymous_RedirectsToLogin()
    {
        var client = NewClient();
        var get = await client.GetAsync("/Einstellungen/Layout");
        Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
        Assert.Contains("/Account/Login", get.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData(Fixtures.BeraterEmail, Fixtures.BeraterPassword)]
    [InlineData(Fixtures.SachbearbeiterEmail, Fixtures.SachbearbeiterPassword)]
    public async Task Layout_NonAdmin_IsDeniedOnGetAndPost_AndThemeUnchanged(string user, string password)
    {
        using var factory = new TestAppFactory();
        var client = await LoginAsync(user, password, factory);
        var token = await TokenAsync(client, "/");

        var get = await client.GetAsync("/Einstellungen/Layout");
        Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
        Assert.Contains("/Account/AccessDenied", get.Headers.Location!.ToString());

        var post = await client.PostAsync("/Einstellungen/Layout", new FormUrlEncodedContent(Form(token, "#aa0000")));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains("/Account/AccessDenied", post.Headers.Location!.ToString());
        var reset = await client.PostAsync("/Einstellungen/LayoutReset", new FormUrlEncodedContent(Form(token, "#aa0000")));
        Assert.Contains("/Account/AccessDenied", reset.Headers.Location!.ToString());

        var css = await NewClient(factory).GetStringAsync("/theme.css");
        Assert.DoesNotContain("#aa0000", css);
        Assert.DoesNotContain("Layout</a>", await client.GetStringAsync("/"));
    }

    [Fact]
    public async Task Admin_SeesLayoutMenu_AndPage()
    {
        using var factory = new TestAppFactory();
        var client = await LoginAsync(Fixtures.AdminEmail, Fixtures.AdminPassword, factory);
        Assert.Contains(">Layout<", await client.GetStringAsync("/"));
        var page = await client.GetStringAsync("/Einstellungen/Layout");
        Assert.Contains("Neustart", page);
        Assert.Contains("themePreview", page);
        Assert.Contains("name=\"values[focus]\"", page);
    }

    [Fact]
    public async Task Admin_AppliesTheme_SharedWithOtherUsers_ThenResets()
    {
        using var factory = new TestAppFactory();
        var admin = await LoginAsync(Fixtures.AdminEmail, Fixtures.AdminPassword, factory);
        var other = await LoginAsync(Fixtures.BeraterEmail, Fixtures.BeraterPassword, factory);

        var token = await TokenAsync(admin, "/Einstellungen/Layout");
        var apply = await admin.PostAsync("/Einstellungen/Layout", new FormUrlEncodedContent(Form(token, "#AA0000")));
        Assert.Equal(HttpStatusCode.Redirect, apply.StatusCode);

        var css = await other.GetAsync("/theme.css");
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Equal("text/css", css.Content.Headers.ContentType!.MediaType);
        Assert.Contains("--app-primary: #aa0000;", await css.Content.ReadAsStringAsync());
        Assert.Contains("/theme.css", await other.GetStringAsync("/"));

        token = await TokenAsync(admin, "/Einstellungen/Layout");
        var reset = await admin.PostAsync("/Einstellungen/LayoutReset", new FormUrlEncodedContent(Form(token, "#aa0000")));
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        var after = await other.GetStringAsync("/theme.css");
        Assert.Contains("--app-primary: #1f4e79;", after);
    }

    [Fact]
    public async Task Admin_InvalidColor_IsRejected_AndNotDelivered()
    {
        using var factory = new TestAppFactory();
        var admin = await LoginAsync(Fixtures.AdminEmail, Fixtures.AdminPassword, factory);
        var token = await TokenAsync(admin, "/Einstellungen/Layout");
        var response = await admin.PostAsync("/Einstellungen/Layout",
            new FormUrlEncodedContent(Form(token, "#fff;} body{display:none")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var css = await NewClient(factory).GetStringAsync("/theme.css");
        Assert.DoesNotContain("display:none", css);
        Assert.Contains("--app-primary: #1f4e79;", css);
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        using var factory = new TestAppFactory();
        var admin = await LoginAsync(Fixtures.AdminEmail, Fixtures.AdminPassword, factory);
        var form = ThemeService.Defaults().ToDictionary(p => $"values[{p.Key}]", p => p.Value);
        var response = await admin.PostAsync("/Einstellungen/Layout", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ThemeCss_IsPublicForLoginPage_AndSiteCssUsesVariables()
    {
        var client = NewClient();
        Assert.Contains("/theme.css", await client.GetStringAsync("/Account/Login"));
        var site = await client.GetStringAsync("/css/site.css");
        Assert.Contains("var(--app-calendar)", site);
        Assert.Contains("var(--app-surface)", site);
    }
}
