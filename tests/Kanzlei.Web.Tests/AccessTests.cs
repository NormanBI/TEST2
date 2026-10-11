using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kanzlei.Web.Tests;

public class AccessTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public AccessTests(TestAppFactory factory) => _factory = factory;

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    private static async Task<string> TokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        return Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    private async Task<HttpClient> LoginAsync(string user, string password)
    {
        var client = NewClient();
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

    [Theory]
    [InlineData("/")]
    [InlineData("/Stammdaten")]
    [InlineData("/Auswertungen")]
    [InlineData("/Einstellungen")]
    public async Task Anonymous_IsRedirectedToLogin(string url)
    {
        var response = await NewClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsError()
    {
        var client = NewClient();
        var token = await TokenAsync(client, "/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserName"] = "admin",
            ["Password"] = "falsch",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ungültig", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Admin_SeesAllMenuItems_AndCanOpenEinstellungen()
    {
        var client = await LoginAsync("admin", "Demo-Admin-1");
        var home = await client.GetStringAsync("/");
        Assert.Contains("id=\"kalender\"", home);
        Assert.Contains(">Stammdaten<", home);
        Assert.Contains(">Auswertungen<", home);
        Assert.Contains(">Einstellungen<", home);
        foreach (var url in new[] { "/Stammdaten", "/Auswertungen", "/Einstellungen" })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
        }
    }

    [Theory]
    [InlineData("steuerberater", "Demo-Steuerberater-1")]
    [InlineData("sachbearbeiter", "Demo-Sachbearbeiter-1")]
    public async Task NonAdmin_HasNoEinstellungen_AndServerDeniesAccess(string user, string password)
    {
        var client = await LoginAsync(user, password);
        var home = await client.GetStringAsync("/");
        Assert.Contains(">Stammdaten<", home);
        Assert.Contains(">Auswertungen<", home);
        Assert.DoesNotContain(">Einstellungen<", home);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Stammdaten")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Auswertungen")).StatusCode);

        var denied = await client.GetAsync("/Einstellungen");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/Account/AccessDenied", denied.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Account/AccessDenied")).StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var client = await LoginAsync("admin", "Demo-Admin-1");
        var token = await TokenAsync(client, "/");
        await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }));
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
