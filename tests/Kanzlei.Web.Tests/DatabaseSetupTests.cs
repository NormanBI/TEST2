using System.Net;
using System.Text.RegularExpressions;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kanzlei.Web.Tests;

public class DatabaseSetupTests
{
    private static TestAppFactory Unconfigured(DbTestOutcome outcome = DbTestOutcome.Ok)
    {
        var f = new TestAppFactory();
        f.Store.Result = new(DbSettingsLoadStatus.Missing, null);
        f.Gateway.Outcome = outcome;
        return f;
    }

    private static HttpClient Client(TestAppFactory f) => f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<HttpResponseMessage> PostAsync(HttpClient c, string url, string password = "S3cret!pw", string db = "kanzlei")
    {
        var html = await c.GetStringAsync("/Setup");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        return await c.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Host"] = "db.example", ["Port"] = "3306", ["Database"] = db, ["UserName"] = "app",
            ["Password"] = password, ["SslMode"] = "Preferred", ["__RequestVerificationToken"] = token,
        }));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Einstellungen")]
    public async Task MissingConfig_RedirectsToSetup_NotLogin(string url)
    {
        using var f = Unconfigured();
        var r = await Client(f).GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Setup", r.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task SetupPage_IsGerman_AndShownAnonymously()
    {
        using var f = Unconfigured();
        var r = await Client(f).GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("Verbindung testen", html);
        Assert.Contains("__RequestVerificationToken", html);
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        using var f = Unconfigured();
        var r = await Client(f).PostAsync("/Setup/Save", new FormUrlEncodedContent(new Dictionary<string, string> { ["Host"] = "x" }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(0, f.Store.SaveCalls);
    }

    [Fact]
    public async Task SuccessfulSave_Persists_ThenLoginIsReachable_AndSetupIsClosed()
    {
        using var f = Unconfigured();
        var c = Client(f);
        var r = await PostAsync(c, "/Setup/Save");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Account/Login", r.Headers.Location!.OriginalString);
        Assert.Equal(1, f.Store.SaveCalls);
        Assert.Equal(1, f.Gateway.MigrateCalls);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Account/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Setup")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync("/Setup/Save", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
    }

    [Fact]
    public async Task TestButton_DoesNotSave()
    {
        using var f = Unconfigured();
        var r = await PostAsync(Client(f), "/Setup/Test");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(0, f.Store.SaveCalls);
        Assert.Contains("Verbindung erfolgreich", await r.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(DbTestOutcome.Unreachable, "nicht erreichbar")]
    [InlineData(DbTestOutcome.InvalidCredentials, "Anmeldung am Datenbankserver fehlgeschlagen")]
    [InlineData(DbTestOutcome.InvalidDatabase, "Datenbank existiert nicht")]
    [InlineData(DbTestOutcome.Timeout, "Zeitüberschreitung")]
    public async Task FailedTest_DoesNotPersist_AndDoesNotLeakPassword(DbTestOutcome outcome, string text)
    {
        using var f = Unconfigured(outcome);
        var c = Client(f);
        var r = await PostAsync(c, "/Setup/Save", password: "TopSecret-123");
        var html = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains(text, WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("TopSecret-123", html);
        Assert.Equal(0, f.Store.SaveCalls);
        Assert.Equal("/Setup", (await c.GetAsync("/Account/Login")).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task InvalidInput_IsRejected_WithoutConnecting()
    {
        using var f = Unconfigured();
        var r = await PostAsync(Client(f), "/Setup/Save", db: "bad;name");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(0, f.Gateway.TestCalls);
        Assert.Equal(0, f.Store.SaveCalls);
    }

    [Fact]
    public async Task Configured_Login_IsReachable_AndSetupIs404()
    {
        using var f = new TestAppFactory();
        var c = Client(f);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Account/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/Setup")).StatusCode);
    }

    [Fact]
    public async Task UnreadableConfig_ShowsRecovery_NotLogin_AndNeverOverwrites()
    {
        using var f = new TestAppFactory();
        f.Store.Result = new(DbSettingsLoadStatus.Unreadable, null);
        var c = Client(f);
        var login = await c.GetAsync("/Account/Login");
        Assert.Equal("/Setup/Status", login.Headers.Location!.OriginalString);
        var status = await c.GetAsync("/Setup/Status");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status.StatusCode);
        Assert.Contains("nicht gelesen", await status.Content.ReadAsStringAsync());
        Assert.Equal("/Setup/Status", (await c.GetAsync("/Setup")).Headers.Location!.OriginalString);
        Assert.Equal(0, f.Store.SaveCalls);
    }

    [Fact]
    public async Task ServerUnavailable_ShowsUnavailablePage_KeepsConfig_AndRecovers()
    {
        using var f = new TestAppFactory();
        f.Gateway.Outcome = DbTestOutcome.Unreachable;
        var c = Client(f);
        Assert.Equal("/Setup/Status", (await c.GetAsync("/Account/Login")).Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync("/Setup/Status")).StatusCode);
        Assert.Equal(DbSettingsLoadStatus.Ok, f.Store.Result.Status);
        Assert.Equal(0, f.Store.SaveCalls);
    }

    [Fact]
    public async Task Restart_ReloadsSavedSettings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kanzlei-db-" + Guid.NewGuid().ToString("N"));
        try
        {
            var provider = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(dir, "keys")));
            var gateway = new FakeGateway();
            var path = Path.Combine(dir, "cfg", "db.json");
            var first = new DatabaseState(new DbSettingsStore(path, provider), gateway, LoggerFactory.Create(_ => { }).CreateLogger<DatabaseState>());
            Assert.Equal(DatabaseStatus.NotConfigured, await first.EnsureReadyAsync(default));
            Assert.True((await first.ConfigureAsync(Fixtures.Sample, default)).Success);

            var restarted = new DatabaseState(new DbSettingsStore(path, DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(dir, "keys"))), null), gateway, LoggerFactory.Create(_ => { }).CreateLogger<DatabaseState>());
            Assert.Equal(DatabaseStatus.Ready, await restarted.EnsureReadyAsync(default));
            Assert.Equal(Fixtures.Sample, restarted.Settings);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task FailedConfigure_DoesNotCreateFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kanzlei-db-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "db.json");
            var state = new DatabaseState(new DbSettingsStore(path, DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(dir, "k")))),
                new FakeGateway { Outcome = DbTestOutcome.InvalidCredentials }, LoggerFactory.Create(_ => { }).CreateLogger<DatabaseState>());
            await state.EnsureReadyAsync(default);
            Assert.False((await state.ConfigureAsync(Fixtures.Sample, default)).Success);
            Assert.False(File.Exists(path));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}

public class DbSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kanzlei-store-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
    private IDataProtectionProvider Provider(string sub = "keys") => DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(_dir, sub)));

    [Fact]
    public void Password_IsEncryptedAtRest_AndNoConnectionString()
    {
        var path = Path.Combine(_dir, "db.json");
        new DbSettingsStore(path, Provider()).Save(Fixtures.Sample);
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("S3cret!pw", text);
        Assert.Contains("ProtectedPassword", text);
        Assert.Contains("db.example", text);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void RoundTrip_WithSameKeyRing()
    {
        var path = Path.Combine(_dir, "db.json");
        new DbSettingsStore(path, Provider()).Save(Fixtures.Sample);
        var r = new DbSettingsStore(path, Provider()).Load();
        Assert.Equal(DbSettingsLoadStatus.Ok, r.Status);
        Assert.Equal(Fixtures.Sample, r.Settings);
    }

    [Fact]
    public void MissingFile_IsMissing() =>
        Assert.Equal(DbSettingsLoadStatus.Missing, new DbSettingsStore(Path.Combine(_dir, "none.json"), Provider()).Load().Status);

    [Fact]
    public void DifferentKeyRing_IsUnreadable_AndFileUntouched()
    {
        var path = Path.Combine(_dir, "db.json");
        new DbSettingsStore(path, Provider("a")).Save(Fixtures.Sample);
        var before = File.ReadAllText(path);
        Assert.Equal(DbSettingsLoadStatus.Unreadable, new DbSettingsStore(path, Provider("b")).Load().Status);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{}")]
    [InlineData("null")]
    public void Malformed_IsUnreadable(string content)
    {
        var path = Path.Combine(_dir, "db.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, content);
        Assert.Equal(DbSettingsLoadStatus.Unreadable, new DbSettingsStore(path, Provider()).Load().Status);
    }

    [Fact]
    public void Redaction_ToStringAndLogsHaveNoSecrets()
    {
        Assert.DoesNotContain("S3cret!pw", Fixtures.Sample.ToString());
        Assert.DoesNotContain("S3cret!pw", new DbTestResult(DbTestOutcome.InvalidCredentials).Message);

        var path = Path.Combine(_dir, "db.json");
        new DbSettingsStore(path, Provider("a")).Save(Fixtures.Sample);
        var sink = new ListLogger();
        new DbSettingsStore(path, Provider("b"), sink).Load();
        Assert.NotEmpty(sink.Lines);
        Assert.All(sink.Lines, l => { Assert.DoesNotContain("S3cret!pw", l); Assert.DoesNotContain("db.example", l); });
    }

    [Fact]
    public void ConnectionString_EscapesInjection()
    {
        var s = Fixtures.Sample with { Password = "a;Database=evil" };
        var cs = new MySqlConnector.MySqlConnectionStringBuilder(s.BuildConnectionString());
        Assert.Equal("a;Database=evil", cs.Password);
        Assert.Equal("kanzlei", cs.Database);
    }

    [Fact]
    public async Task PathConfiguration_UsesConfiguredFileAndKeyPaths()
    {
        var cfg = Path.Combine(_dir, "custom", "db.json");
        var keys = Path.Combine(_dir, "custom-keys");
        var gateway = new FakeGateway();
        using var f = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:ConfigFile", cfg);
            b.UseSetting("DataProtection:KeysPath", keys);
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IDatabaseGateway>(); s.AddSingleton<IDatabaseGateway>(gateway);
            });
        });
        var c = f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var html = await c.GetStringAsync("/Setup");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var r = await c.PostAsync("/Setup/Save", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Host"] = "h", ["Port"] = "3306", ["Database"] = "d", ["UserName"] = "u", ["Password"] = "PathPw-1",
            ["SslMode"] = "None", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.True(File.Exists(cfg));
        Assert.DoesNotContain("PathPw-1", File.ReadAllText(cfg));
        Assert.NotEmpty(Directory.GetFiles(keys, "key-*.xml"));
    }

    private sealed class ListLogger : ILogger<DbSettingsStore>
    {
        public List<string> Lines { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception) + exception?.Message);
    }
}
