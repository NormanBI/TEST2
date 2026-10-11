using System.Net;
using System.Text.RegularExpressions;
using Kanzlei.Web.Data;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Kanzlei.Web.Tests;

public class EinstellungenSchemaTests
{
    private static AppDbContext MySqlContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseMySql("Server=localhost;Database=design_time", new MySqlServerVersion(new Version(8, 0, 36))).Options);

    [Fact]
    public void Model_HasTableColumnsAndSingletonConstraint()
    {
        using var db = MySqlContext();
        var e = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Einstellungen))!;
        Assert.Equal("Einstellungen", e.GetTableName());
        foreach (var n in new[] { "Id", "Navigationstext", "ErstelltAm", "GeaendertAm" }) Assert.NotNull(e.FindProperty(n));
        Assert.Equal(4, e.GetProperties().Count());
        Assert.Contains(e.GetCheckConstraints(), c => c.Name == "CK_Einstellungen_Singleton");
    }

    [Fact]
    public void Migration_IsAdditive()
    {
        using var db = MySqlContext();
        var migrations = db.GetService<IMigrationsAssembly>().Migrations;
        var up = db.GetService<IMigrator>().GenerateScript(fromMigration: migrations.Keys.First(k => k.EndsWith("AddMailEinstellungen")));
        Assert.Contains("CREATE TABLE `Einstellungen`", up);
        Assert.DoesNotContain("DROP TABLE", up);
        Assert.DoesNotContain("ALTER TABLE", up);
    }
}

public class BrandingServiceTests
{
    private sealed class FailingFactory : IAppDbContextFactory
    {
        public AppDbContext Create() => throw new InvalidOperationException("DB down");
    }

    [Fact]
    public async Task NoRow_UsesDefault_AndEnsureRowIsIdempotent()
    {
        var ctx = new InMemoryDbContextFactory();
        var svc = new EfBrandingService(ctx);
        Assert.Equal("Kanzlei-Prototyp", await svc.GetBrandTextAsync());
        await svc.EnsureRowAsync(default);
        await svc.EnsureRowAsync(default);
        using var db = ctx.Create();
        var row = db.Einstellungen.Single();
        Assert.Equal(Einstellungen.SingletonId, row.Id);
        Assert.Equal("Kanzlei-Prototyp", row.Navigationstext);
    }

    [Fact]
    public async Task EnsureRow_DoesNotOverwriteExistingText()
    {
        var ctx = new InMemoryDbContextFactory();
        await new EfBrandingService(ctx).SaveAsync("Meine Kanzlei", default);
        await new EfBrandingService(ctx).EnsureRowAsync(default);
        Assert.Equal("Meine Kanzlei", await new EfBrandingService(ctx).GetBrandTextAsync());
    }

    [Fact]
    public async Task Save_PersistsTrimmed_AndIsRetrievedAfterServiceRecreation()
    {
        var ctx = new InMemoryDbContextFactory();
        var svc = new EfBrandingService(ctx);
        Assert.Equal(BrandingSaveResult.Ok, await svc.SaveAsync("  Müller und Partner ", default));
        Assert.Equal("Müller und Partner", await svc.GetBrandTextAsync());
        Assert.Equal("Müller und Partner", await new EfBrandingService(ctx).GetBrandTextAsync());
        using var db = ctx.Create();
        Assert.Single(db.Einstellungen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<b>Fett</b>")]
    [InlineData("A&B")]
    [InlineData("Zeile\nUmbruch")]
    [InlineData("Tab\there")]
    public async Task Save_RejectsInvalid_AndKeepsOldValue(string? input)
    {
        var ctx = new InMemoryDbContextFactory();
        var svc = new EfBrandingService(ctx);
        await svc.SaveAsync("Alt", default);
        Assert.Equal(BrandingSaveResult.Invalid, await svc.SaveAsync(input, default));
        Assert.Equal("Alt", await new EfBrandingService(ctx).GetBrandTextAsync());
    }

    [Fact]
    public void Validation_LengthLimit()
    {
        Assert.True(BrandingValidation.TryNormalize(new string('a', 80), out _, out _));
        Assert.False(BrandingValidation.TryNormalize(new string('a', 81), out _, out var error));
        Assert.Contains("80", error);
    }

    [Fact]
    public async Task DatabaseOutage_FallsBackToDefault_WithoutThrowing()
    {
        var svc = new EfBrandingService(new FailingFactory());
        Assert.Equal("Kanzlei-Prototyp", await svc.GetBrandTextAsync());
        Assert.Equal(BrandingSaveResult.StorageFailed, await svc.SaveAsync("Neu", default));
        Assert.Equal("Kanzlei-Prototyp", await svc.GetBrandTextAsync());
    }

    [Fact]
    public async Task Value_IsCached_NoQueryPerRead()
    {
        var ctx = new InMemoryDbContextFactory();
        await new EfBrandingService(ctx).SaveAsync("Cache", default);
        var svc = new EfBrandingService(ctx);
        Assert.Equal("Cache", await svc.GetBrandTextAsync());
        using (var db = ctx.Create()) { db.Einstellungen.Single().Navigationstext = "Direkt"; db.SaveChanges(); }
        Assert.Equal("Cache", await svc.GetBrandTextAsync());
    }
}

public class BrandingEndpointTests
{
    private static HttpClient Client(TestAppFactory f) => f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<string> Token(HttpClient c, string url)
    {
        var html = await c.GetStringAsync(url);
        return Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    private static async Task<HttpClient> Login(TestAppFactory f, string user = Fixtures.AdminEmail, string pw = Fixtures.AdminPassword)
    {
        var c = Client(f);
        var r = await c.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["UserName"] = user, ["Password"] = pw, ["__RequestVerificationToken"] = await Token(c, "/Account/Login") }));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        return c;
    }

    private static async Task<HttpResponseMessage> PostText(HttpClient c, string? text)
    {
        var form = new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token(c, "/Einstellungen/Layout") };
        if (text is not null) form["navigationstext"] = text;
        return await c.PostAsync("/Einstellungen/Navigationstext", new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task Startup_SeedsRow_AndDefaultIsShownOnAllPages()
    {
        using var f = new TestAppFactory();
        var admin = await Login(f);
        using (var db = f.Contexts.Create()) Assert.Equal("Kanzlei-Prototyp", db.Einstellungen.Single().Navigationstext);
        foreach (var url in new[] { "/", "/Stammdaten", "/Auswertungen", "/Einstellungen", "/Einstellungen/Layout", "/Benutzer", "/Einstellungen/Mailserver" })
            Assert.Contains(">Kanzlei-Prototyp</a>", await admin.GetStringAsync(url));
        Assert.Contains(">Kanzlei-Prototyp</a>", await Client(f).GetStringAsync("/Account/Login"));
    }

    [Fact]
    public async Task Admin_Saves_AndAllPagesShowEncodedValue_AfterRestart()
    {
        using var f = new TestAppFactory();
        var admin = await Login(f);
        var r = await PostText(admin, "  Kanzlei Müller ");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        foreach (var url in new[] { "/", "/Stammdaten", "/Auswertungen", "/Einstellungen", "/Einstellungen/Layout" })
        {
            var html = await admin.GetStringAsync(url);
            Assert.Contains(">Kanzlei Müller</a>".Replace("ü", "&#xFC;"), html);
            Assert.DoesNotContain(">Kanzlei-Prototyp</a>", html);
        }
        Assert.Contains("Kanzlei M&#xFC;ller", await Client(f).GetStringAsync("/Account/Login"));
        // "Neustart": neuer Dienst über dieselbe Datenbank
        Assert.Equal("Kanzlei Müller", await new EfBrandingService(f.Contexts).GetBrandTextAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("a\u0001b")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task Invalid_IsRejected_WithGermanMessage_AndNothingStored(string text)
    {
        using var f = new TestAppFactory();
        var admin = await Login(f);
        var r = await PostText(admin, text);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("invalid-feedback", html);
        Assert.DoesNotContain("<script>alert", html);
        Assert.Matches("Bitte einen Text|höchstens 80 Zeichen|Steuerzeichen|kein HTML", html);
        using var db = f.Contexts.Create();
        Assert.Equal("Kanzlei-Prototyp", db.Einstellungen.Single().Navigationstext);
    }

    [Fact]
    public async Task StoredMarkupInDatabase_IsHtmlEncoded_OnOutput()
    {
        using var f = new TestAppFactory();
        using (var db = f.Contexts.Create()) { db.Einstellungen.Add(new Einstellungen { Navigationstext = "<img src=x onerror=alert(1)>" }); db.SaveChanges(); }
        var admin = await Login(f);
        var html = await admin.GetStringAsync("/");
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains(">Kanzlei-Prototyp</a>", html);
    }

    [Fact]
    public async Task EditEndpoint_IsAdminOnly_AndRequiresAntiforgery()
    {
        using var f = new TestAppFactory();
        var anon = Client(f);
        var r0 = await anon.PostAsync("/Einstellungen/Navigationstext", new FormUrlEncodedContent(new Dictionary<string, string> { ["navigationstext"] = "Hack" }));
        Assert.NotEqual(HttpStatusCode.OK, r0.StatusCode);
        var berater = await Login(f, Fixtures.BeraterEmail, Fixtures.BeraterPassword);
        var token = await Token(berater, "/");
        var r = await berater.PostAsync("/Einstellungen/Navigationstext", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["navigationstext"] = "Hack", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/AccessDenied", r.Headers.Location!.OriginalString);

        var admin = await Login(f);
        var noToken = await admin.PostAsync("/Einstellungen/Navigationstext", new FormUrlEncodedContent(new Dictionary<string, string> { ["navigationstext"] = "Hack" }));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal("Kanzlei-Prototyp", await new EfBrandingService(f.Contexts).GetBrandTextAsync());
    }
}
