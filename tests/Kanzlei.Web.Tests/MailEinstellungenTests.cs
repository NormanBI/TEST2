using System.Net;
using System.Text.RegularExpressions;
using Kanzlei.Web.Data;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kanzlei.Web.Tests;

public class MailSchemaTests
{
    private static AppDbContext MySqlContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseMySql("Server=localhost;Database=design_time", new MySqlServerVersion(new Version(8, 0, 36))).Options);

    [Fact]
    public void Model_HasTableColumnsAndSingletonConstraint()
    {
        using var db = MySqlContext();
        var e = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(MailEinstellungen))!;
        Assert.Equal("MailEinstellungen", e.GetTableName());
        foreach (var n in new[] { "SmtpHost", "SmtpPort", "Benutzername", "PasswortVerschluesselt", "AbsenderEmail", "AbsenderName", "SslModus", "Aktiv", "Konfiguriert", "ErstelltAm", "GeaendertAm" })
            Assert.NotNull(e.FindProperty(n));
        Assert.Null(e.FindProperty("Passwort"));
        Assert.Contains(e.GetCheckConstraints(), c => c.Name == "CK_MailEinstellungen_Singleton");
    }

    [Fact]
    public void Migration_IsAdditive()
    {
        using var db = MySqlContext();
        var migrations = db.GetService<IMigrationsAssembly>().Migrations;
        Assert.Contains(migrations.Keys, k => k.EndsWith("AddMailEinstellungen"));
        var up = db.GetService<IMigrator>().GenerateScript(fromMigration: migrations.Keys.First(k => k.EndsWith("AddBenutzer")));
        Assert.Contains("CREATE TABLE `MailEinstellungen`", up);
        Assert.DoesNotContain("DROP TABLE", up);
        Assert.DoesNotContain("ALTER TABLE `Benutzer`", up);
    }
}

public class MailStoreTests : IDisposable
{
    private const string Secret = "Smtp-G3heim!";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kanzlei-mail-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
    private IDataProtectionProvider Provider(string sub = "keys") => DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(_dir, sub)));

    private static MailSettings Sample(string? password = Secret) =>
        new("smtp.example", 587, "mailuser", password, "noreply@kanzlei.test", "Kanzlei", MailSslModus.StartTls);

    [Fact]
    public async Task Save_EncryptsPassword_AndReloadsAfterRecreation()
    {
        var ctx = new InMemoryDbContextFactory();
        await new EfMailSettingsStore(ctx, Provider()).SaveAsync(Sample(), default);
        using (var db = ctx.Create())
        {
            var row = db.MailEinstellungen.Single();
            Assert.NotNull(row.PasswortVerschluesselt);
            Assert.DoesNotContain(Secret, row.PasswortVerschluesselt);
            Assert.True(row.Konfiguriert);
            Assert.Equal(MailEinstellungen.SingletonId, row.Id);
        }
        var load = await new EfMailSettingsStore(ctx, Provider()).LoadAsync(default);
        Assert.Equal(MailSettingsLoadStatus.Ok, load.Status);
        Assert.Equal(Sample(), load.Settings);
    }

    [Fact]
    public async Task Save_Twice_KeepsSingleRow_AndNullPasswordKeepsCiphertext()
    {
        var ctx = new InMemoryDbContextFactory();
        var store = new EfMailSettingsStore(ctx, Provider());
        await store.SaveAsync(Sample(), default);
        string before; using (var db = ctx.Create()) before = db.MailEinstellungen.Single().PasswortVerschluesselt!;
        Assert.Equal(MailSaveResult.Ok, await store.SaveAsync(Sample(null) with { SenderName = "Neu" }, default));
        using var db2 = ctx.Create();
        var row = db2.MailEinstellungen.Single();
        Assert.Equal(before, row.PasswortVerschluesselt);
        Assert.Equal("Neu", row.AbsenderName);
        Assert.Equal(Secret, (await store.LoadAsync(default)).Settings!.Password);
    }

    [Fact]
    public async Task DifferentKeyRing_IsUnreadable_AndNotOverwritten()
    {
        var ctx = new InMemoryDbContextFactory();
        await new EfMailSettingsStore(ctx, Provider("a")).SaveAsync(Sample(), default);
        string before; using (var db = ctx.Create()) before = db.MailEinstellungen.Single().PasswortVerschluesselt!;
        var other = new EfMailSettingsStore(ctx, Provider("b"));
        Assert.Equal(MailSettingsLoadStatus.Unreadable, (await other.LoadAsync(default)).Status);
        Assert.Equal(MailSaveResult.Unreadable, await other.SaveAsync(Sample(null), default));
        using var db2 = ctx.Create();
        Assert.Equal(before, db2.MailEinstellungen.Single().PasswortVerschluesselt);
    }

    [Fact]
    public async Task Missing_And_NotConfigured_AreMissing()
    {
        var ctx = new InMemoryDbContextFactory();
        var store = new EfMailSettingsStore(ctx, Provider());
        Assert.Equal(MailSettingsLoadStatus.Missing, (await store.LoadAsync(default)).Status);
        ctx.AddMail(configured: false);
        Assert.Equal(MailSettingsLoadStatus.Missing, (await store.LoadAsync(default)).Status);
    }

    [Fact]
    public void ToString_Results_AndLogs_HaveNoSecrets()
    {
        Assert.DoesNotContain(Secret, Sample().ToString());
        Assert.DoesNotContain("mailuser", Sample().ToString());
        foreach (var o in Enum.GetValues<MailTestOutcome>())
            Assert.DoesNotContain(Secret, new MailTestResult(o).Message);
    }

    [Fact]
    public void Validation_RequiresTlsForCredentials()
    {
        Assert.True(Sample().IsValid());
        Assert.False((Sample() with { Tls = MailSslModus.Keine }).IsValid());
        Assert.True((Sample() with { Tls = MailSslModus.Keine, UserName = null, Password = null }).IsValid());
        Assert.False((Sample() with { SenderEmail = "kein-mail" }).IsValid());
        Assert.False((Sample() with { Port = 0 }).IsValid());
        Assert.False((Sample() with { SenderName = "A\r\nBcc: x" }).IsValid());
    }
}

public class MailStateAndEndpointTests
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

    private static Dictionary<string, string> Form(string pw = "Neu-G3heim!", string user = "mailuser", string tls = "StartTls") => new()
    {
        ["Host"] = "smtp.example", ["Port"] = "587", ["UserName"] = user, ["Password"] = pw, ["Tls"] = tls,
        ["SenderEmail"] = "noreply@kanzlei.test", ["SenderName"] = "Kanzlei",
    };

    private static async Task<HttpResponseMessage> Post(HttpClient c, string page, string url, Dictionary<string, string> f)
    {
        f["__RequestVerificationToken"] = await Token(c, page);
        return await c.PostAsync(url, new FormUrlEncodedContent(f));
    }

    private static TestAppFactory NoMail(bool users = true) => new() { SeedMail = false, SeedUsers = users };

    [Fact]
    public async Task DbSetup_IsStillFirst_BeforeMail()
    {
        using var f = NoMail(users: false);
        f.Store.Result = new(DbSettingsLoadStatus.Missing, null);
        var c = Client(f);
        Assert.Equal("/Setup", (await c.GetAsync("/Account/Login")).Headers.Location!.OriginalString);
        Assert.Equal("/Setup", (await c.GetAsync("/Setup/Mail")).Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Setup/Admin")]
    [InlineData("/Setup")]
    [InlineData("/Setup/Status")]
    public async Task MissingMail_RedirectsToMailSetup_BeforeAdminAndLogin(string url)
    {
        using var f = NoMail(users: false);
        var r = await Client(f).GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Setup/Mail", r.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task MailSetupPage_IsGerman_Responsive_AndAnonymous()
    {
        using var f = NoMail();
        var r = await Client(f).GetAsync("/Setup/Mail");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("Verbindung testen", html);
        Assert.Contains("col-12 col-sm-8", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Equal(HttpStatusCode.OK, (await Client(f).GetAsync("/theme.css")).StatusCode);
    }

    [Fact]
    public async Task Save_AfterSuccessfulTest_PersistsEncrypted_ThenAdminSetup()
    {
        using var f = NoMail(users: false);
        var c = Client(f);
        var r = await Post(c, "/Setup/Mail", "/Setup/Mail", Form());
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Setup/Admin", r.Headers.Location!.OriginalString);
        Assert.Equal(1, f.MailTester.Calls);
        using var db = f.Contexts.Create();
        var row = db.MailEinstellungen.Single();
        Assert.True(row.Konfiguriert);
        Assert.DoesNotContain("Neu-G3heim!", row.PasswortVerschluesselt);
        // Mail-Setup ist danach nicht mehr erreichbar.
        Assert.Equal("/Setup/Admin", (await c.GetAsync("/Setup/Mail")).Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Setup/Admin")).StatusCode);
    }

    [Fact]
    public async Task Save_WithAdminPresent_GoesToLogin()
    {
        using var f = NoMail(users: true);
        var r = await Post(Client(f), "/Setup/Mail", "/Setup/Mail", Form());
        Assert.Equal("/Account/Login", r.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData(MailTestOutcome.AuthenticationFailed)]
    [InlineData(MailTestOutcome.ConnectionFailed)]
    [InlineData(MailTestOutcome.TlsFailed)]
    [InlineData(MailTestOutcome.Timeout)]
    public async Task FailedTest_DoesNotPersist_AndDoesNotEchoSecret(MailTestOutcome outcome)
    {
        using var f = NoMail();
        f.MailTester.Outcome = outcome;
        var r = await Post(Client(f), "/Setup/Mail", "/Setup/Mail", Form(pw: "Geheim-XYZ-1"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains(new MailTestResult(outcome).Message, WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("Geheim-XYZ-1", html);
        using var db = f.Contexts.Create();
        Assert.Empty(db.MailEinstellungen);
        Assert.Equal("/Setup/Mail", (await Client(f).GetAsync("/")).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task TestOnly_DoesNotSave()
    {
        using var f = NoMail();
        var r = await Post(Client(f), "/Setup/Mail", "/Setup/MailTest", Form());
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Verbindung erfolgreich", await r.Content.ReadAsStringAsync());
        using var db = f.Contexts.Create();
        Assert.Empty(db.MailEinstellungen);
    }

    [Fact]
    public async Task InvalidInput_IsRejected_WithoutTesting()
    {
        using var f = NoMail();
        var c = Client(f);
        var bad = Form(tls: "Keine");
        var r = await Post(c, "/Setup/Mail", "/Setup/Mail", bad);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Mit Anmeldedaten ist eine Verschlüsselung", WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
        var bad2 = Form(); bad2["SenderEmail"] = "kaputt";
        await Post(c, "/Setup/Mail", "/Setup/Mail", bad2);
        Assert.Equal(0, f.MailTester.Calls);
    }

    [Fact]
    public async Task Post_WithoutAntiforgery_IsRejected()
    {
        using var f = NoMail();
        var r = await Client(f).PostAsync("/Setup/Mail", new FormUrlEncodedContent(Form()));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(0, f.MailTester.Calls);
    }

    [Fact]
    public async Task OversizedRequest_IsRejected()
    {
        using var f = NoMail();
        var c = Client(f);
        var form = Form(); form["SenderName"] = new string('x', 20000);
        var r = await Post(c, "/Setup/Mail", "/Setup/Mail", form);
        Assert.NotEqual(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(0, f.MailTester.Calls);
    }

    [Fact]
    public async Task UnreadableMail_ShowsStatus_AndNeverOverwrites()
    {
        using var f = NoMail();
        f.Contexts.AddMail("mailuser", "kein-gueltiges-chiffrat");
        var c = Client(f);
        var r = await c.GetAsync("/Account/Login");
        Assert.Equal("/Setup/Status", r.Headers.Location!.OriginalString);
        var status = await c.GetAsync("/Setup/Status");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status.StatusCode);
        Assert.Contains("nicht lesbar", await status.Content.ReadAsStringAsync());
        Assert.Equal("/Setup/Status", (await c.GetAsync("/Setup/Mail")).Headers.Location!.OriginalString);
        using var db = f.Contexts.Create();
        Assert.Equal("kein-gueltiges-chiffrat", db.MailEinstellungen.Single().PasswortVerschluesselt);
    }

    [Fact]
    public async Task SettingsPage_IsAdminOnly_OnEveryAction()
    {
        using var f = new TestAppFactory();
        var anon = Client(f);
        Assert.Equal(HttpStatusCode.Redirect, (await anon.GetAsync("/Einstellungen/Mailserver")).StatusCode);
        var berater = await Login(f, Fixtures.BeraterEmail, Fixtures.BeraterPassword);
        Assert.Equal(HttpStatusCode.Redirect, (await berater.GetAsync("/Einstellungen/Mailserver")).StatusCode);
        var token = await Token(berater, "/");
        foreach (var url in new[] { "/Einstellungen/Mailserver", "/Einstellungen/MailserverTest" })
        {
            var form = Form(); form["__RequestVerificationToken"] = token;
            var r = await berater.PostAsync(url, new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.Contains("/Account/AccessDenied", r.Headers.Location!.OriginalString);
        }
        Assert.Equal(0, f.MailTester.Calls);
        var admin = await Login(f);
        var html = await admin.GetStringAsync("/Einstellungen/Mailserver");
        Assert.Contains("Mailserver", html);
        Assert.Contains(">Mailserver<", await admin.GetStringAsync("/"));
        Assert.DoesNotContain(">Mailserver<", await berater.GetStringAsync("/"));
    }

    [Fact]
    public async Task Edit_BlankPassword_PreservesCiphertext_UsesStoredForTest_AndNeverRendersSecret()
    {
        using var f = new TestAppFactory();
        var store = f.Services.GetRequiredService<IMailSettingsStore>();
        await store.SaveAsync(new("smtp.example", 587, "mailuser", "Alt-G3heim!", "noreply@kanzlei.test", "Kanzlei", MailSslModus.StartTls), default);
        string cipher; using (var db = f.Contexts.Create()) cipher = db.MailEinstellungen.Single().PasswortVerschluesselt!;

        var admin = await Login(f);
        var page = await admin.GetStringAsync("/Einstellungen/Mailserver");
        Assert.DoesNotContain("Alt-G3heim!", page);
        Assert.DoesNotContain(cipher, page);
        Assert.Contains("Leer lassen, um es beizubehalten", page);

        var form = Form(pw: ""); form["SenderName"] = "Neue Kanzlei";
        var r = await Post(admin, "/Einstellungen/Mailserver", "/Einstellungen/Mailserver", form);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("Alt-G3heim!", f.MailTester.Last!.Password);
        using var db2 = f.Contexts.Create();
        var row = db2.MailEinstellungen.Single();
        Assert.Equal(cipher, row.PasswortVerschluesselt);
        Assert.Equal("Neue Kanzlei", row.AbsenderName);
    }

    [Fact]
    public async Task Edit_FailedTest_KeepsOldSettings()
    {
        using var f = new TestAppFactory();
        var admin = await Login(f);
        f.MailTester.Outcome = MailTestOutcome.ConnectionFailed;
        var form = Form(pw: "Nochmal-G3heim!", user: ""); form["Host"] = "anders.example";
        var r = await Post(admin, "/Einstellungen/Mailserver", "/Einstellungen/Mailserver", form);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Nochmal-G3heim!", html);
        using var db = f.Contexts.Create();
        Assert.Equal("smtp.example", db.MailEinstellungen.Single().SmtpHost);
    }

    [Fact]
    public async Task Edit_Validation_RejectsMissingPasswordForNewCredentials()
    {
        using var f = new TestAppFactory();
        var admin = await Login(f);
        var r = await Post(admin, "/Einstellungen/Mailserver", "/Einstellungen/Mailserver", Form(pw: ""));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Bitte ein Passwort angeben", WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()));
        Assert.Equal(0, f.MailTester.Calls);
    }

    [Fact]
    public async Task Edit_WithUnreadableSettings_FailsClosed_AndNeverOverwrites()
    {
        using var f = new TestAppFactory();
        var admin = await Login(f);
        using (var db = f.Contexts.Create())
        {
            var row = db.MailEinstellungen.Single();
            row.Benutzername = "mailuser"; row.PasswortVerschluesselt = "kein-gueltiges-chiffrat";
            db.SaveChanges();
        }
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await admin.GetAsync("/Einstellungen/Mailserver")).StatusCode);
        var r = await Post(admin, "/", "/Einstellungen/Mailserver", Form(pw: "Neu-G3heim!"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.Equal(0, f.MailTester.Calls);
        using var db2 = f.Contexts.Create();
        Assert.Equal("kein-gueltiges-chiffrat", db2.MailEinstellungen.Single().PasswortVerschluesselt);
    }
}

public class MailStateTests
{
    private sealed class ThrowingStore : IMailSettingsStore
    {
        public Task<MailSettingsLoadResult> LoadAsync(CancellationToken ct) => throw new InvalidOperationException("db down");
        public Task<MailSaveResult> SaveAsync(MailSettings s, CancellationToken ct) => throw new InvalidOperationException("db down");
    }

    [Fact]
    public async Task StorageUnavailable_IsNotTreatedAsMissingMail()
    {
        var f = new TestAppFactory { SeedMail = false };
        f.Store.Result = new(DbSettingsLoadStatus.Ok, Fixtures.Sample);
        var state = new DatabaseState(f.Store, f.Gateway, LoggerFactory.Create(_ => { }).CreateLogger<DatabaseState>(), f.Users, new ThrowingStore(), f.MailTester);
        Assert.Equal(DatabaseStatus.Unavailable, await state.EnsureReadyAsync(default));
    }
}
