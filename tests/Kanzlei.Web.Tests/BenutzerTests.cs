using System.Net;
using System.Text.RegularExpressions;
using Kanzlei.Web.Data;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kanzlei.Web.Tests;

public class BenutzerSchemaTests
{
    private static AppDbContext MySqlContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseMySql("Server=localhost;Database=design_time", new MySqlServerVersion(new Version(8, 0, 36))).Options);

    [Fact]
    public void Model_HasRequiredColumns_UniqueEmailIndex_AndDefaults()
    {
        using var db = MySqlContext();
        var e = db.Model.FindEntityType(typeof(Benutzer))!;
        Assert.Equal("Benutzer", e.GetTableName());
        foreach (var name in new[] { "Vorname", "Nachname", "Emailadresse", "PasswortHash" })
            Assert.False(e.FindProperty(name)!.IsNullable);
        Assert.Contains(e.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(Benutzer.EmailNormalisiert));
        Assert.Equal(false, e.FindProperty(nameof(Benutzer.Kundennutzer))!.GetDefaultValue());
        Assert.Equal(true, e.FindProperty(nameof(Benutzer.Aktiv))!.GetDefaultValue());
        Assert.Null(e.FindProperty("Passwort"));
    }

    [Fact]
    public void NewEntity_Defaults()
    {
        var b = new Benutzer();
        Assert.False(b.Kundennutzer);
        Assert.True(b.Aktiv);
        Assert.False(b.Administrator || b.Eigentuemer || b.Steuerberater || b.Sachbearbeiter);
    }

    [Fact]
    public void Migration_CreatesBenutzerTable_WithoutDataLossOperations()
    {
        using var db = MySqlContext();
        var migrations = db.GetService<IMigrationsAssembly>().Migrations;
        Assert.Contains(migrations.Keys, k => k.EndsWith("AddBenutzer"));
        Assert.Contains(migrations.Keys, k => k.EndsWith("InitialCreate"));
        var script = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE `Benutzer`", script);
        Assert.Contains("UNIQUE INDEX `IX_Benutzer_EmailNormalisiert`", script);
        Assert.DoesNotContain("DROP TABLE", script);
    }
}

public class BenutzerStoreTests
{
    private static (EfBenutzerStore Store, InMemoryDbContextFactory Ctx) Create()
    {
        var ctx = new InMemoryDbContextFactory();
        return (new EfBenutzerStore(ctx, new PasswordHasher<Benutzer>()), ctx);
    }

    private static NeuerBenutzer Neu(string email = "a@b.test", string pw = "Geheimes-Passwort-1", bool admin = false) =>
        new("Anna", "Muster", email, pw, false, admin, false, false, false);

    [Fact]
    public async Task Hash_DiffersFromPlaintext_AndVerifies()
    {
        var (store, ctx) = Create();
        await store.CreateAsync(Neu(pw: "Geheimes-Passwort-1"), default);
        using var db = ctx.Create();
        var u = db.Benutzer.Single();
        Assert.NotEqual("Geheimes-Passwort-1", u.PasswortHash);
        Assert.DoesNotContain("Geheimes", u.PasswortHash);
        var h = new PasswordHasher<Benutzer>();
        Assert.Equal(PasswordVerificationResult.Success, h.VerifyHashedPassword(u, u.PasswortHash, "Geheimes-Passwort-1"));
        Assert.Equal(PasswordVerificationResult.Failed, h.VerifyHashedPassword(u, u.PasswortHash, "falsch"));
    }

    [Fact]
    public async Task SameEmail_CaseInsensitive_IsDuplicate()
    {
        var (store, _) = Create();
        Assert.Equal(BenutzerResult.Ok, await store.CreateAsync(Neu("Anna@Test.de"), default));
        Assert.Equal(BenutzerResult.DuplicateEmail, await store.CreateAsync(Neu(" anna@test.DE "), default));
        Assert.Single(await store.ListAsync(default));
    }

    [Fact]
    public async Task InitialAdmin_IsIdempotent()
    {
        var (store, _) = Create();
        Assert.False(await store.HasActiveAdministratorAsync(default));
        Assert.Equal(BenutzerResult.Ok, await store.CreateInitialAdministratorAsync(Neu("root@test.de"), default));
        Assert.Equal(BenutzerResult.AdministratorExists, await store.CreateInitialAdministratorAsync(Neu("root2@test.de"), default));
        var list = await store.ListAsync(default);
        Assert.Single(list);
        Assert.True(list[0].Administrator);
        Assert.True(list[0].Aktiv);
    }

    [Fact]
    public async Task Authenticate_MapsRoles_NotKundennutzer_AndHandlesNoRoles()
    {
        var (store, _) = Create();
        await store.CreateAsync(new NeuerBenutzer("A", "B", "multi@test.de", "Geheimes-Passwort-1", true, true, true, true, true), default);
        await store.CreateAsync(new NeuerBenutzer("C", "D", "none@test.de", "Geheimes-Passwort-1", true, false, false, false, false), default);
        var multi = await store.AuthenticateAsync("MULTI@test.de", "Geheimes-Passwort-1", default);
        Assert.Equal(new[] { Roles.Administration, Roles.Eigentuemer, Roles.Steuerberater, Roles.Sachbearbeiter }, multi!.Rollen);
        var none = await store.AuthenticateAsync("none@test.de", "Geheimes-Passwort-1", default);
        Assert.NotNull(none);
        Assert.Empty(none!.Rollen);
        Assert.Null(await store.AuthenticateAsync("none@test.de", "falsch", default));
        Assert.Null(await store.AuthenticateAsync("unbekannt@test.de", "Geheimes-Passwort-1", default));
    }

    [Fact]
    public async Task Deactivated_CannotAuthenticate_ReactivationWorks()
    {
        var (store, _) = Create();
        await store.CreateAsync(Neu("admin@test.de", admin: true), default);
        await store.CreateAsync(Neu("u@test.de"), default);
        var id = (await store.ListAsync(default)).Single(u => u.Emailadresse == "u@test.de").Id;
        Assert.Equal(BenutzerResult.Ok, await store.SetActiveAsync(id, false, default));
        Assert.False(await store.IsActiveAsync(id, default));
        Assert.Null(await store.AuthenticateAsync("u@test.de", "Geheimes-Passwort-1", default));
        Assert.Equal(BenutzerResult.Ok, await store.SetActiveAsync(id, true, default));
        Assert.NotNull(await store.AuthenticateAsync("u@test.de", "Geheimes-Passwort-1", default));
        Assert.Equal(BenutzerResult.NotFound, await store.SetActiveAsync(9999, false, default));
    }

    [Fact]
    public async Task LastActiveAdministrator_CannotBeDeactivated()
    {
        var (store, _) = Create();
        await store.CreateAsync(Neu("a1@test.de", admin: true), default);
        await store.CreateAsync(Neu("a2@test.de", admin: true), default);
        var users = await store.ListAsync(default);
        Assert.Equal(BenutzerResult.Ok, await store.SetActiveAsync(users[0].Id, false, default));
        Assert.Equal(BenutzerResult.LastAdministrator, await store.SetActiveAsync(users[1].Id, false, default));
        Assert.True(await store.HasActiveAdministratorAsync(default));
    }

    [Theory]
    [InlineData("kurz1A", false)]
    [InlineData("nurkleinbuchstaben", false)]
    [InlineData("OHNEZIFFERNKLEIN", false)]
    [InlineData("Gueltiges-Passwort-1", true)]
    public void PasswordPolicy_Validates(string pw, bool ok) => Assert.Equal(ok, PasswordPolicy.Validate(pw) is null);
}

public class BenutzerEndpointTests
{
    private static HttpClient Client(TestAppFactory f) => f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<string> Token(HttpClient c, string url)
    {
        var html = await c.GetStringAsync(url);
        return Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> LoginPost(HttpClient c, string user, string pw) =>
        await c.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["UserName"] = user, ["Password"] = pw, ["__RequestVerificationToken"] = await Token(c, "/Account/Login") }));

    private static async Task<HttpClient> Login(TestAppFactory f, string user = Fixtures.AdminEmail, string pw = Fixtures.AdminPassword)
    {
        var c = Client(f);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginPost(c, user, pw)).StatusCode);
        return c;
    }

    private static async Task<HttpResponseMessage> PostForm(HttpClient c, string page, string url, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await Token(c, page);
        return await c.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    private static Dictionary<string, string> NewUser(string email = "neu@kanzlei.test", string pw = "Neues-Passwort-1") => new()
    {
        ["Vorname"] = "Nina", ["Nachname"] = "Neu", ["Emailadresse"] = email, ["Passwort"] = pw,
        ["Kundennutzer"] = "true", ["Sachbearbeiter"] = "true",
    };

    [Fact]
    public async Task Menu_ShowsBenutzer_OnlyForAdmin()
    {
        using var f = new TestAppFactory();
        Assert.Contains(">Benutzer<", await (await Login(f)).GetStringAsync("/"));
        Assert.DoesNotContain(">Benutzer<", await (await Login(f, Fixtures.BeraterEmail, Fixtures.BeraterPassword)).GetStringAsync("/"));
    }

    [Theory]
    [InlineData("/Benutzer")]
    [InlineData("/Benutzer/Create")]
    public async Task NonAdmin_AndAnonymous_CannotOpenPages(string url)
    {
        using var f = new TestAppFactory();
        Assert.Contains("/Account/Login", (await Client(f).GetAsync(url)).Headers.Location!.ToString());
        var c = await Login(f, Fixtures.BeraterEmail, Fixtures.BeraterPassword);
        Assert.Contains("/Account/AccessDenied", (await c.GetAsync(url)).Headers.Location!.ToString());
    }

    [Fact]
    public async Task NonAdmin_CannotPostDirectly()
    {
        using var f = new TestAppFactory();
        var c = await Login(f, Fixtures.BeraterEmail, Fixtures.BeraterPassword);
        var token = await Token(c, "/");
        var r = await c.PostAsync("/Benutzer/Create", new FormUrlEncodedContent(new Dictionary<string, string>(NewUser()) { ["__RequestVerificationToken"] = token }));
        Assert.Contains("/Account/AccessDenied", r.Headers.Location!.ToString());
        Assert.Single(await f.Users.ListAsync(default), u => u.Emailadresse == Fixtures.AdminEmail);
        Assert.Equal(3, (await f.Users.ListAsync(default)).Count);
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        using var f = new TestAppFactory();
        var c = await Login(f);
        var r = await c.PostAsync("/Benutzer/Create", new FormUrlEncodedContent(NewUser()));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(3, (await f.Users.ListAsync(default)).Count);
    }

    [Fact]
    public async Task Admin_CreatesUser_ListShowsIt_WithoutSecrets_AndNewUserCanLogin()
    {
        using var f = new TestAppFactory();
        var c = await Login(f);
        var r = await PostForm(c, "/Benutzer/Create", "/Benutzer/Create", NewUser());
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/Benutzer"));
        Assert.Contains("neu@kanzlei.test", html);
        Assert.Contains("Kundennutzer", html);
        Assert.DoesNotContain("Neues-Passwort-1", html);
        using (var db = f.Contexts.Create())
            Assert.DoesNotContain(db.Benutzer.Single(b => b.Vorname == "Nina").PasswortHash, html);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginPost(Client(f), "NEU@kanzlei.test", "Neues-Passwort-1")).StatusCode);
    }

    [Fact]
    public async Task Create_ValidatesInput_WithGermanErrors_AndNeverEchoesPassword()
    {
        using var f = new TestAppFactory();
        var c = await Login(f);
        var r = await PostForm(c, "/Benutzer/Create", "/Benutzer/Create", new() { ["Vorname"] = "", ["Nachname"] = "", ["Emailadresse"] = "kaputt", ["Passwort"] = "zukurz" });
        var html = WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("Bitte einen Vornamen angeben.", html);
        Assert.Contains("Bitte eine gültige E-Mail-Adresse angeben.", html);
        Assert.DoesNotContain("zukurz", html);

        r = await PostForm(c, "/Benutzer/Create", "/Benutzer/Create", NewUser(pw: "alleskleinohneziffer"));
        Assert.Contains("Das Passwort muss Groß- und Kleinbuchstaben sowie mindestens eine Ziffer enthalten.", WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Create_DuplicateEmail_ShowsFieldError()
    {
        using var f = new TestAppFactory();
        var c = await Login(f);
        var r = await PostForm(c, "/Benutzer/Create", "/Benutzer/Create", NewUser(email: Fixtures.BeraterEmail.ToUpperInvariant()));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("bereits verwendet", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Deactivate_BlocksLogin_EndsExistingSession_AndCanBeReactivated()
    {
        using var f = new TestAppFactory();
        var admin = await Login(f);
        var berater = await Login(f, Fixtures.BeraterEmail, Fixtures.BeraterPassword);
        Assert.Equal(HttpStatusCode.OK, (await berater.GetAsync("/Stammdaten")).StatusCode);
        var id = (await f.Users.ListAsync(default)).Single(u => u.Emailadresse == Fixtures.BeraterEmail).Id;

        Assert.Equal(HttpStatusCode.Redirect, (await PostForm(admin, "/Benutzer", $"/Benutzer/Deaktivieren/{id}", new())).StatusCode);

        Assert.Contains("/Account/Login", (await berater.GetAsync("/Stammdaten")).Headers.Location!.ToString());
        var again = Client(f);
        Assert.Equal(HttpStatusCode.OK, (await LoginPost(again, Fixtures.BeraterEmail, Fixtures.BeraterPassword)).StatusCode);

        await PostForm(admin, "/Benutzer", $"/Benutzer/Aktivieren/{id}", new());
        Assert.Equal(HttpStatusCode.Redirect, (await LoginPost(again, Fixtures.BeraterEmail, Fixtures.BeraterPassword)).StatusCode);
    }

    [Fact]
    public async Task LastAdministrator_CannotDeactivateSelf()
    {
        using var f = new TestAppFactory();
        var admin = await Login(f);
        var id = (await f.Users.ListAsync(default)).Single(u => u.Administrator).Id;
        var r = await PostForm(admin, "/Benutzer", $"/Benutzer/Deaktivieren/{id}", new());
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("letzte aktive Administrator", WebUtility.HtmlDecode(await admin.GetStringAsync("/Benutzer")));
        Assert.True(await f.Users.IsActiveAsync(id, default));
    }

    [Theory]
    [InlineData("/Benutzer/Delete/1")]
    [InlineData("/Benutzer/Loeschen/1")]
    [InlineData("/Benutzer/Remove/1")]
    public async Task NoDeleteRoute_Exists(string url)
    {
        using var f = new TestAppFactory();
        var c = await Login(f);
        var r = await PostForm(c, "/Benutzer", url, new());
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        var html = await c.GetStringAsync("/Benutzer");
        Assert.DoesNotContain("Löschen", html);
        Assert.DoesNotContain("Delete", html);
    }

    [Fact]
    public async Task FirstRun_WithoutAdministrator_RequiresAdminSetup_ThenLoginWorks()
    {
        using var f = new TestAppFactory { SeedUsers = false };
        var c = Client(f);
        var r = await c.GetAsync("/Account/Login");
        Assert.Equal("/Setup/Admin", r.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Setup/Admin")).StatusCode);

        var bad = await PostForm(c, "/Setup/Admin", "/Setup/Admin", new() { ["Vorname"] = "A", ["Nachname"] = "B", ["Emailadresse"] = "root@test.de", ["Passwort"] = "Geheimes-Passwort-1", ["PasswortWiederholung"] = "Anderes-Passwort-2" });
        Assert.Contains("stimmen nicht überein", WebUtility.HtmlDecode(await bad.Content.ReadAsStringAsync()));
        Assert.Empty(await f.Users.ListAsync(default));

        var ok = await PostForm(c, "/Setup/Admin", "/Setup/Admin", new() { ["Vorname"] = "A", ["Nachname"] = "B", ["Emailadresse"] = "root@test.de", ["Passwort"] = "Geheimes-Passwort-1", ["PasswortWiederholung"] = "Geheimes-Passwort-1" });
        Assert.Equal("/Account/Login", ok.Headers.Location!.OriginalString);
        Assert.True((await f.Users.ListAsync(default)).Single().Administrator);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Setup/Admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginPost(Client(f), "root@test.de", "Geheimes-Passwort-1")).StatusCode);
    }

    [Fact]
    public async Task AdminSetup_IsNotReachable_WhenAdministratorExists()
    {
        using var f = new TestAppFactory();
        var r = await Client(f).PostAsync("/Setup/Admin", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
}
