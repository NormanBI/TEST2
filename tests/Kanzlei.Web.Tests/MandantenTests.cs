using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

public class MasterClientsSchemaTests
{
    private static AppDbContext MySqlContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseMySql("Server=localhost;Database=design_time", new MySqlServerVersion(new Version(8, 0, 36))).Options);

    [Fact]
    public void Model_MapsExactTableAndColumns()
    {
        using var db = MySqlContext();
        var e = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(MasterClient))!;
        Assert.Equal("Master_Clients", e.GetTableName());
        Assert.Equal("PK_Master_Clients", e.FindPrimaryKey()!.GetName());
        var table = StoreObjectIdentifier.Table("Master_Clients");
        // (Spalte, Nullable, Typ)
        var expected = new (string Col, bool Nullable, string Type)[]
        {
            ("id", false, "varchar(450)"), ("client_since", true, "longtext"), ("client_to", true, "longtext"),
            ("differing_name", true, "longtext"), ("legal_person_id", true, "longtext"), ("name", false, "longtext"),
            ("natural_person_id", true, "longtext"), ("note", true, "longtext"), ("number", false, "bigint"),
            ("status", true, "longtext"), ("timestamp", true, "longtext"), ("type", false, "longtext"),
            ("organization_id", true, "longtext"), ("organization_name", true, "longtext"), ("organization_number", true, "bigint"),
            ("establishment_id", true, "longtext"), ("establishment_name", true, "longtext"), ("establishment_number", true, "int"),
            ("establishment_short_name", true, "longtext"), ("establishment_area_id", true, "longtext"),
            ("functional_area_id", true, "longtext"), ("functional_area_name", true, "longtext"), ("functional_area_short_name", true, "longtext"),
        };
        Assert.Equal(expected.Length, e.GetProperties().Count());
        foreach (var (col, nullable, type) in expected)
        {
            var p = e.GetProperties().Single(x => x.GetColumnName(table) == col);
            Assert.Equal(nullable, p.IsNullable);
            Assert.Equal(type, p.GetColumnType());
        }
        Assert.Equal(450, e.GetProperties().Single(x => x.GetColumnName(table) == "id").GetMaxLength());
        Assert.Equal(typeof(long), e.GetProperties().Single(x => x.GetColumnName(table) == "number").ClrType);
        Assert.Equal(typeof(long?), e.GetProperties().Single(x => x.GetColumnName(table) == "organization_number").ClrType);
        Assert.Equal(typeof(int?), e.GetProperties().Single(x => x.GetColumnName(table) == "establishment_number").ClrType);
    }

    [Fact]
    public void Model_LayoutTable_HasUniqueUserIndexAndForeignKey()
    {
        using var db = MySqlContext();
        var e = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(BenutzerDashboardLayout))!;
        Assert.Contains(e.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(BenutzerDashboardLayout.BenutzerId));
        Assert.Contains(e.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(Benutzer));
    }

    [Fact]
    public void Migration_IsAdditive_AndSeedsTwoRows()
    {
        using var db = MySqlContext();
        var migrations = db.GetService<IMigrationsAssembly>().Migrations;
        var up = db.GetService<IMigrator>().GenerateScript(fromMigration: migrations.Keys.First(k => k.EndsWith("AddEinstellungen")));
        Assert.Contains("CREATE TABLE `Master_Clients`", up);
        Assert.Contains("CREATE TABLE `BenutzerDashboardLayouts`", up);
        Assert.DoesNotContain("DROP TABLE", up);
        Assert.DoesNotContain("ALTER TABLE `Benutzer`", up);
        Assert.Equal(1, Regex.Matches(up, "INSERT INTO `Master_Clients`").Count);
        Assert.Equal(2, Regex.Matches(up, @"\('demo-mandant-\d{4}'").Count);
    }

    [Fact]
    public void Seed_HasTwoFictionalRows_AndRepeatedInitializationDoesNotDuplicate()
    {
        var ctx = new InMemoryDbContextFactory();
        for (var i = 0; i < 3; i++) { using var db = ctx.Create(); db.Database.EnsureCreated(); }
        using var check = ctx.Create();
        var rows = check.MasterClients.OrderBy(c => c.Id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.StartsWith("demo-mandant-", r.Id));
        Assert.Equal(2, rows.Select(r => r.Status).Distinct().Count());
        Assert.Equal(2, rows.Select(r => r.Type).Distinct().Count());
    }
}

public class MandantenAuswertungTests
{
    private static InMemoryDbContextFactory Create(params (string? Status, string Typ)[] rows)
    {
        var ctx = new InMemoryDbContextFactory();
        using var db = ctx.Create();
        var n = 0;
        foreach (var (s, t) in rows) db.MasterClients.Add(new MasterClient { Id = "x" + n, Name = "N" + n, Number = n++, Status = s, Type = t });
        db.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task Aggregates_GroupDeterministically_IncludingNullsAndBlanks()
    {
        var svc = new EfMandantenAuswertungService(Create(("Aktiv", "A"), ("Aktiv", "B"), (null, "A"), ("  ", "A"), ("Ruhend", "")));
        var r = (await svc.BerechnenAsync(null, null, default))!;
        Assert.Equal(new[] { ("", 2), ("Aktiv", 2), ("Ruhend", 1) }, r.Status.Select(k => (k.Schluessel, k.Anzahl)).ToArray());
        Assert.Equal("Ohne Status", r.Status[0].Bezeichnung);
        Assert.Equal(new[] { ("A", 3), ("", 1), ("B", 1) }, r.Typ.Select(k => (k.Schluessel, k.Anzahl)).ToArray());
        Assert.Equal("Ohne Typ", r.Typ[1].Bezeichnung);
    }

    [Fact]
    public async Task CrossFilter_RecomputesTheOtherChart()
    {
        var svc = new EfMandantenAuswertungService(Create(("Aktiv", "A"), ("Aktiv", "B"), (null, "A"), ("Ruhend", "B")));
        var byStatus = (await svc.BerechnenAsync("Aktiv", null, default))!;
        Assert.Equal(new[] { ("A", 1), ("B", 1) }, byStatus.Typ.Select(k => (k.Schluessel, k.Anzahl)).ToArray());
        Assert.Equal(3, byStatus.Status.Count); // Statusdiagramm bleibt ungefiltert
        var byTyp = (await svc.BerechnenAsync(null, "B", default))!;
        Assert.Equal(new[] { ("Aktiv", 1), ("Ruhend", 1) }, byTyp.Status.Select(k => (k.Schluessel, k.Anzahl)).ToArray());
        var ohne = (await svc.BerechnenAsync("", null, default))!;
        Assert.Equal(new[] { ("A", 1) }, ohne.Typ.Select(k => (k.Schluessel, k.Anzahl)).ToArray());
    }

    [Fact]
    public async Task UnknownFilter_IsRejected()
    {
        var svc = new EfMandantenAuswertungService(Create(("Aktiv", "A")));
        Assert.Null(await svc.BerechnenAsync("<script>", null, default));
        Assert.Null(await svc.BerechnenAsync(null, "Z", default));
    }
}

public class DashboardLayoutStoreTests
{
    private static async Task<InMemoryDbContextFactory> WithUsers()
    {
        var ctx = new InMemoryDbContextFactory();
        ctx.Add("A", "A", "a@x.test", "Passwort-1234"); ctx.Add("B", "B", "b@x.test", "Passwort-1234");
        await using var db = ctx.Create();
        Assert.Equal(2, await db.Benutzer.CountAsync());
        return ctx;
    }

    private static PanelLayout[] Custom(int sx = 4) => new[]
    {
        new PanelLayout(MandantenLayout.StatusPanel, 0, 0, sx, 6), new PanelLayout(MandantenLayout.TypPanel, 6, 0, 12, 3),
    };

    [Fact]
    public async Task NewUser_GetsDefaultTwoTileLayout()
    {
        var store = new EfDashboardLayoutStore(await WithUsers());
        Assert.Equal(MandantenLayout.Standard, await store.LoadAsync(1, default));
        Assert.Equal(2, MandantenLayout.Standard.Count);
    }

    [Fact]
    public async Task SaveRestore_IsolatedPerUser_AndUpdatesInPlace()
    {
        var ctx = await WithUsers();
        var store = new EfDashboardLayoutStore(ctx);
        Assert.Equal(LayoutSaveResult.Ok, await store.SaveAsync(1, Custom(4), default));
        Assert.Equal(LayoutSaveResult.Ok, await store.SaveAsync(1, Custom(5), default));
        Assert.Equal(5, (await new EfDashboardLayoutStore(ctx).LoadAsync(1, default)).Single(p => p.Id == MandantenLayout.StatusPanel).SizeX);
        Assert.Equal(MandantenLayout.Standard, await store.LoadAsync(2, default));
        await using var db = ctx.Create();
        Assert.Equal(1, await db.BenutzerDashboardLayouts.CountAsync());
        await store.ResetAsync(1, default);
        Assert.Equal(MandantenLayout.Standard, await store.LoadAsync(1, default));
    }

    [Fact]
    public async Task Save_UnknownUser_IsRejected()
    {
        var store = new EfDashboardLayoutStore(await WithUsers());
        Assert.Equal(LayoutSaveResult.UserNotFound, await store.SaveAsync(99, Custom(), default));
    }

    public static IEnumerable<object[]> Invalid() => new[]
    {
        new object[] { new[] { new PanelLayout("panel-status", 0, 0, 6, 5) } },
        new object[] { new[] { new PanelLayout("panel-status", 0, 0, 6, 5), new PanelLayout("<script>", 0, 6, 6, 5) } },
        new object[] { new[] { new PanelLayout("panel-status", 0, 0, 6, 5), new PanelLayout("panel-status", 0, 6, 6, 5) } },
        new object[] { new[] { new PanelLayout("panel-status", -1, 0, 6, 5), new PanelLayout("panel-typ", 0, 6, 6, 5) } },
        new object[] { new[] { new PanelLayout("panel-status", 0, 0, 13, 5), new PanelLayout("panel-typ", 9, 0, 6, 5) } },
        new object[] { new[] { new PanelLayout("panel-status", 0, 0, 6, 0), new PanelLayout("panel-typ", 0, 6, 6, 5) } },
        new object[] { new[] { new PanelLayout("panel-status", 0, 8, 6, 5), new PanelLayout("panel-typ", 0, 6, 6, 5) } },
        new object[] { new[] { new PanelLayout("panel-status", 0, 0, 6, 5), new PanelLayout("panel-typ", 2, 3, 6, 5) } },
        new object[] { new[] { new PanelLayout("panel-status", 0, 0, 6, int.MaxValue), new PanelLayout("panel-typ", 0, 6, 6, 5) } },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public async Task Save_RejectsInvalid(PanelLayout[] panels)
    {
        var store = new EfDashboardLayoutStore(await WithUsers());
        Assert.Equal(LayoutSaveResult.Invalid, await store.SaveAsync(1, panels, default));
        Assert.Equal(MandantenLayout.Standard, await store.LoadAsync(1, default));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"a\":1}")]
    [InlineData("[{\"id\":\"panel-status\",\"row\":0,\"col\":0,\"sizeX\":99,\"sizeY\":1},{\"id\":\"panel-typ\",\"row\":0,\"col\":6,\"sizeX\":6,\"sizeY\":5}]")]
    public async Task MalformedStoredPreferences_FallBackToDefault(string json)
    {
        var ctx = await WithUsers();
        await using (var db = ctx.Create())
        {
            db.BenutzerDashboardLayouts.Add(new BenutzerDashboardLayout { BenutzerId = 1, LayoutJson = json });
            await db.SaveChangesAsync();
        }
        Assert.Equal(MandantenLayout.Standard, await new EfDashboardLayoutStore(ctx).LoadAsync(1, default));
    }
}

public class MandantenEndpointTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    public MandantenEndpointTests(TestAppFactory factory) => _factory = factory;

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private async Task<HttpClient> LoginAsync(string user, string password)
    {
        var client = NewClient();
        var html = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["UserName"] = user, ["Password"] = password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static HttpRequestMessage Post(string url, string json, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (token is not null) req.Headers.Add("RequestVerificationToken", token);
        return req;
    }

    private static async Task<string> TokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/Auswertungen/Mandanten");
        return Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }

    private const string Valid = "[{\"id\":\"panel-status\",\"row\":0,\"col\":0,\"sizeX\":8,\"sizeY\":6},{\"id\":\"panel-typ\",\"row\":6,\"col\":0,\"sizeX\":12,\"sizeY\":4}]";

    [Theory]
    [InlineData("/Auswertungen/Mandanten")]
    [InlineData("/Auswertungen/Mandanten/Daten")]
    public async Task Anonymous_IsRedirectedToLogin(string url)
    {
        var r = await NewClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/Login", r.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Anonymous_CannotSaveLayout()
    {
        var r = await NewClient().SendAsync(Post("/Auswertungen/Mandanten/Layout", Valid, null));
        Assert.NotEqual(HttpStatusCode.NoContent, r.StatusCode);
    }

    [Theory]
    [InlineData(Fixtures.AdminEmail, Fixtures.AdminPassword)]
    [InlineData(Fixtures.BeraterEmail, Fixtures.BeraterPassword)]
    [InlineData(Fixtures.SachbearbeiterEmail, Fixtures.SachbearbeiterPassword)]
    public async Task AuthorizedRoles_SeePageDataAndNavigation(string user, string pw)
    {
        var client = await LoginAsync(user, pw);
        var page = await client.GetStringAsync("/Auswertungen/Mandanten");
        Assert.Contains("mandanten-dashboard", page);
        Assert.Contains("panel-status", page);
        Assert.Contains("panel-typ", page);
        Assert.Contains("chart-status", page);
        Assert.Contains("chart-typ", page);
        Assert.Contains("\\u003cdiv id=\\u0022chart-status\\u0022", page);
        Assert.Contains("Filter zurücksetzen", page);
        var home = await client.GetStringAsync("/");
        Assert.Contains("href=\"/Auswertungen/Mandanten\"", home);
        Assert.Contains(">Stammdaten<", home);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Auswertungen")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Stammdaten")).StatusCode);

        var json = JsonDocument.Parse(await client.GetStringAsync("/Auswertungen/Mandanten/Daten")).RootElement;
        Assert.Equal(2, json.GetProperty("status").GetArrayLength());
        Assert.Equal(2, json.GetProperty("typ").GetArrayLength());
        Assert.False(json.GetProperty("status")[0].TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Daten_FiltersByKnownValue_AndRejectsUnknown()
    {
        var client = await LoginAsync(Fixtures.AdminEmail, Fixtures.AdminPassword);
        var ok = await client.GetStringAsync("/Auswertungen/Mandanten/Daten?status=Aktiv");
        var typ = JsonDocument.Parse(ok).RootElement.GetProperty("typ");
        Assert.Equal(1, typ.GetArrayLength());
        Assert.Equal("Juristische Person", typ[0].GetProperty("schluessel").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Auswertungen/Mandanten/Daten?status=%3Cscript%3E")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Auswertungen/Mandanten/Daten?typ=")).StatusCode);
    }

    [Fact]
    public async Task SaveLayout_RequiresAntiforgery_Validates_AndRestoresOnlyForOwner()
    {
        var admin = await LoginAsync(Fixtures.AdminEmail, Fixtures.AdminPassword);
        var berater = await LoginAsync(Fixtures.BeraterEmail, Fixtures.BeraterPassword);
        var token = await TokenAsync(admin);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.SendAsync(Post("/Auswertungen/Mandanten/Layout", Valid, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.SendAsync(Post("/Auswertungen/Mandanten/Layout", "[{\"id\":\"<img>\"}]", token))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.SendAsync(Post("/Auswertungen/Mandanten/Layout", "kaputt", token))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.SendAsync(Post("/Auswertungen/Mandanten/Layout", Valid, token))).StatusCode);

        Assert.Contains("\"sizeX\": 8", await admin.GetStringAsync("/Auswertungen/Mandanten"));
        Assert.DoesNotContain("\"sizeX\": 8", await berater.GetStringAsync("/Auswertungen/Mandanten"));
    }
}
