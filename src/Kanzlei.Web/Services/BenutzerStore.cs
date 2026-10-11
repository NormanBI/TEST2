using Kanzlei.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kanzlei.Web.Services;

// Listenansicht ohne Passwort-Hash.
public sealed record BenutzerListItem(int Id, string Vorname, string Nachname, string Emailadresse, bool Kundennutzer,
    bool Administrator, bool Eigentuemer, bool Steuerberater, bool Sachbearbeiter, bool Aktiv, DateTime ErstelltAm);

public sealed record NeuerBenutzer(string Vorname, string Nachname, string Emailadresse, string Passwort, bool Kundennutzer,
    bool Administrator, bool Eigentuemer, bool Steuerberater, bool Sachbearbeiter);

public enum BenutzerResult { Ok, NotFound, DuplicateEmail, LastAdministrator, AdministratorExists }

// Angemeldeter, aktiver Benutzer (nur Identitätsdaten, nie der Hash).
public sealed record AuthentifizierterBenutzer(int Id, string Anzeigename, IReadOnlyList<string> Rollen);

public interface IBenutzerStore
{
    Task<bool> HasActiveAdministratorAsync(CancellationToken ct);
    Task<IReadOnlyList<BenutzerListItem>> ListAsync(CancellationToken ct);
    Task<BenutzerResult> CreateAsync(NeuerBenutzer neu, CancellationToken ct);
    // Legt den ersten Administrator nur an, wenn noch kein aktiver Administrator existiert (idempotent).
    Task<BenutzerResult> CreateInitialAdministratorAsync(NeuerBenutzer neu, CancellationToken ct);
    Task<BenutzerResult> SetActiveAsync(int id, bool active, CancellationToken ct);
    Task<AuthentifizierterBenutzer?> AuthenticateAsync(string? email, string? password, CancellationToken ct);
    // Prüft, ob das Konto noch existiert und aktiv ist (Sitzungsvalidierung).
    Task<bool> IsActiveAsync(int id, CancellationToken ct);
}

public interface IAppDbContextFactory
{
    AppDbContext Create();
}

// Erzeugt Kontexte aus der gespeicherten, verschlüsselten Verbindungskonfiguration.
public sealed class MySqlAppDbContextFactory : IAppDbContextFactory
{
    private readonly IDbSettingsStore _store;
    private string? _serverVersion;
    private string? _versionFor;
    public MySqlAppDbContextFactory(IDbSettingsStore store) => _store = store;

    public AppDbContext Create()
    {
        var load = _store.Load();
        if (load.Settings is null) throw new InvalidOperationException("Keine Datenbankkonfiguration vorhanden.");
        var cs = load.Settings.BuildConnectionString();
        if (_serverVersion is null || _versionFor != cs)
        {
            _serverVersion = ServerVersion.AutoDetect(cs).ToString();
            _versionFor = cs;
        }
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(cs, ServerVersion.Parse(_serverVersion)).Options);
    }
}

public sealed class EfBenutzerStore : IBenutzerStore
{
    // Serialisiert Änderungen, damit die Regel "letzter aktiver Administrator" innerhalb einer Instanz nicht durch Parallelität umgangen wird.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string DummyHashSource = "Dummy-Passwort-1";

    private readonly IAppDbContextFactory _factory;
    private readonly IPasswordHasher<Benutzer> _hasher;
    private readonly string _dummyHash;

    public EfBenutzerStore(IAppDbContextFactory factory, IPasswordHasher<Benutzer> hasher)
    {
        _factory = factory; _hasher = hasher;
        _dummyHash = hasher.HashPassword(new Benutzer(), DummyHashSource);
    }

    public async Task<bool> HasActiveAdministratorAsync(CancellationToken ct)
    {
        await using var db = _factory.Create();
        return await db.Benutzer.AnyAsync(b => b.Administrator && b.Aktiv, ct);
    }

    public async Task<IReadOnlyList<BenutzerListItem>> ListAsync(CancellationToken ct)
    {
        await using var db = _factory.Create();
        return await db.Benutzer.AsNoTracking().OrderBy(b => b.Nachname).ThenBy(b => b.Vorname).ThenBy(b => b.Id)
            .Select(b => new BenutzerListItem(b.Id, b.Vorname, b.Nachname, b.Emailadresse, b.Kundennutzer,
                b.Administrator, b.Eigentuemer, b.Steuerberater, b.Sachbearbeiter, b.Aktiv, b.ErstelltAm))
            .ToListAsync(ct);
    }

    public async Task<BenutzerResult> CreateAsync(NeuerBenutzer neu, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try { return await InsertAsync(neu, ct); }
        finally { Gate.Release(); }
    }

    public async Task<BenutzerResult> CreateInitialAdministratorAsync(NeuerBenutzer neu, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            if (await HasActiveAdministratorAsync(ct)) return BenutzerResult.AdministratorExists;
            return await InsertAsync(neu with { Administrator = true }, ct);
        }
        finally { Gate.Release(); }
    }

    private async Task<BenutzerResult> InsertAsync(NeuerBenutzer neu, CancellationToken ct)
    {
        await using var db = _factory.Create();
        var normalized = Benutzer.Normalisieren(neu.Emailadresse);
        if (await db.Benutzer.AnyAsync(b => b.EmailNormalisiert == normalized, ct)) return BenutzerResult.DuplicateEmail;
        var now = DateTime.UtcNow;
        var user = new Benutzer
        {
            Vorname = neu.Vorname.Trim(), Nachname = neu.Nachname.Trim(),
            Emailadresse = neu.Emailadresse.Trim(), EmailNormalisiert = normalized,
            Kundennutzer = neu.Kundennutzer, Administrator = neu.Administrator, Eigentuemer = neu.Eigentuemer,
            Steuerberater = neu.Steuerberater, Sachbearbeiter = neu.Sachbearbeiter,
            Aktiv = true, ErstelltAm = now, GeaendertAm = now,
        };
        user.PasswortHash = _hasher.HashPassword(user, neu.Passwort);
        db.Benutzer.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Eindeutiger Index als letzte Absicherung bei parallelen Instanzen.
            if (await IsDuplicateAsync(normalized, ct)) return BenutzerResult.DuplicateEmail;
            throw;
        }
        return BenutzerResult.Ok;
    }

    private async Task<bool> IsDuplicateAsync(string normalized, CancellationToken ct)
    {
        await using var db = _factory.Create();
        return await db.Benutzer.AnyAsync(b => b.EmailNormalisiert == normalized, ct);
    }

    public async Task<BenutzerResult> SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            await using var db = _factory.Create();
            var user = await db.Benutzer.FirstOrDefaultAsync(b => b.Id == id, ct);
            if (user is null) return BenutzerResult.NotFound;
            if (user.Aktiv == active) return BenutzerResult.Ok;
            if (!active && user.Administrator)
            {
                var others = await db.Benutzer.CountAsync(b => b.Administrator && b.Aktiv && b.Id != id, ct);
                if (others == 0) return BenutzerResult.LastAdministrator;
            }
            user.Aktiv = active;
            user.GeaendertAm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return BenutzerResult.Ok;
        }
        finally { Gate.Release(); }
    }

    public async Task<AuthentifizierterBenutzer?> AuthenticateAsync(string? email, string? password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || password is null) return null;
        var normalized = Benutzer.Normalisieren(email);
        await using var db = _factory.Create();
        var user = await db.Benutzer.AsNoTracking().FirstOrDefaultAsync(b => b.EmailNormalisiert == normalized, ct);
        // Auch bei unbekanntem Konto ein Hash-Vergleich, damit die Antwortzeit keine Konten verrät.
        var result = _hasher.VerifyHashedPassword(user ?? new Benutzer(), user?.PasswortHash ?? _dummyHash, password);
        if (user is null || !user.Aktiv || result == PasswordVerificationResult.Failed) return null;
        return new AuthentifizierterBenutzer(user.Id, $"{user.Vorname} {user.Nachname}".Trim(), Roles.For(user).ToList());
    }

    public async Task<bool> IsActiveAsync(int id, CancellationToken ct)
    {
        await using var db = _factory.Create();
        return await db.Benutzer.AsNoTracking().AnyAsync(b => b.Id == id && b.Aktiv, ct);
    }
}
