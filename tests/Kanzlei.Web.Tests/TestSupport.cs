using Kanzlei.Web.Data;
using Kanzlei.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kanzlei.Web.Tests;

public sealed class FakeGateway : IDatabaseGateway
{
    public DbTestOutcome Outcome { get; set; } = DbTestOutcome.Ok;
    public int TestCalls, MigrateCalls;
    public Task<DbTestResult> TestConnectionAsync(DbConnectionSettings s, CancellationToken ct)
    { TestCalls++; return Task.FromResult(new DbTestResult(Outcome, Outcome == DbTestOutcome.Ok ? "8.0.36" : null)); }
    public Task<DbTestResult> MigrateAsync(DbConnectionSettings s, string? v, CancellationToken ct)
    { MigrateCalls++; return Task.FromResult(new DbTestResult(Outcome, v)); }
}

public sealed class MemoryStore : IDbSettingsStore
{
    public DbSettingsLoadResult Result { get; set; } = new(DbSettingsLoadStatus.Missing, null);
    public int SaveCalls;
    public void Save(DbConnectionSettings s) { SaveCalls++; Result = new(DbSettingsLoadStatus.Ok, s); }
    public DbSettingsLoadResult Load() => Result;
}

public static class Fixtures
{
    public const string AdminEmail = "admin@kanzlei.test";
    public const string AdminPassword = "Admin-Passwort-1";
    public const string BeraterEmail = "berater@kanzlei.test";
    public const string BeraterPassword = "Berater-Passwort-1";
    public const string SachbearbeiterEmail = "sachbearbeiter@kanzlei.test";
    public const string SachbearbeiterPassword = "Sachbearbeiter-Passwort-1";
    public static readonly DbConnectionSettings Sample = new("db.example", 3306, "kanzlei", "app", "S3cret!pw", DbSslMode.Preferred);
}

// EF-Core-InMemory statt MySQL, damit die echte EfBenutzerStore-Logik ohne Server testbar ist.
public sealed class InMemoryDbContextFactory : IAppDbContextFactory
{
    private readonly string _name = Guid.NewGuid().ToString("N");
    private readonly InMemoryDatabaseRoot _root = new();
    public AppDbContext Create() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_name, _root)
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)).Options);

    public void Add(string vorname, string nachname, string email, string password, bool administrator = false, bool steuerberater = false, bool sachbearbeiter = false)
    {
        using var db = Create();
        var user = new Benutzer
        {
            Vorname = vorname, Nachname = nachname, Emailadresse = email, EmailNormalisiert = Benutzer.Normalisieren(email),
            Administrator = administrator, Steuerberater = steuerberater, Sachbearbeiter = sachbearbeiter,
        };
        user.PasswortHash = new PasswordHasher<Benutzer>().HashPassword(user, password);
        db.Benutzer.Add(user);
        db.SaveChanges();
    }
}

// Fabrik mit bereits "konfigurierter" Fake-Datenbank, damit bestehende Tests ohne MySQL laufen (Anmeldung erreichbar).
public class TestAppFactory : WebApplicationFactory<Program>
{
    public FakeGateway Gateway { get; } = new();
    public MemoryStore Store { get; } = new() { Result = new(DbSettingsLoadStatus.Ok, Fixtures.Sample) };

    public InMemoryDbContextFactory Contexts { get; } = new();
    public bool SeedUsers { get; set; } = true;

    public IBenutzerStore Users => Services.GetRequiredService<IBenutzerStore>();

    private bool _seeded;

    // Direkt vor dem Anwendungsstart seeden, damit der Start-Check bereits einen Administrator findet.
    private void SeedOnce()
    {
        if (_seeded || !SeedUsers) return;
        _seeded = true;
        Contexts.Add("Ada", "Admin", Fixtures.AdminEmail, Fixtures.AdminPassword, administrator: true);
        Contexts.Add("Bert", "Berater", Fixtures.BeraterEmail, Fixtures.BeraterPassword, steuerberater: true);
        Contexts.Add("Sabine", "Sachbearbeiter", Fixtures.SachbearbeiterEmail, Fixtures.SachbearbeiterPassword, sachbearbeiter: true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(s =>
        {
            SeedOnce();
            s.RemoveAll<IDatabaseGateway>(); s.RemoveAll<IDbSettingsStore>(); s.RemoveAll<IDatabaseState>();
            s.RemoveAll<IAppDbContextFactory>();
            s.AddSingleton<IAppDbContextFactory>(Contexts);
            s.AddSingleton<IDatabaseGateway>(Gateway);
            s.AddSingleton<IDbSettingsStore>(Store);
            s.AddSingleton<IDatabaseState, DatabaseState>();
        });
}
