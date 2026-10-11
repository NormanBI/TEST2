using Kanzlei.Web.Services;
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
    public static readonly DbConnectionSettings Sample = new("db.example", 3306, "kanzlei", "app", "S3cret!pw", DbSslMode.Preferred);
}

// Fabrik mit bereits "konfigurierter" Fake-Datenbank, damit bestehende Tests ohne MySQL laufen (Anmeldung erreichbar).
public class TestAppFactory : WebApplicationFactory<Program>
{
    public FakeGateway Gateway { get; } = new();
    public MemoryStore Store { get; } = new() { Result = new(DbSettingsLoadStatus.Ok, Fixtures.Sample) };

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<IDatabaseGateway>(); s.RemoveAll<IDbSettingsStore>(); s.RemoveAll<IDatabaseState>();
            s.AddSingleton<IDatabaseGateway>(Gateway);
            s.AddSingleton<IDbSettingsStore>(Store);
            s.AddSingleton<IDatabaseState, DatabaseState>();
        });
}
