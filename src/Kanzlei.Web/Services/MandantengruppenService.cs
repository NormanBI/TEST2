using Kanzlei.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Kanzlei.Web.Services;

public sealed record GruppeInfo(int Id, string Name, int Anzahl);
public sealed record GruppenClient(string Id, long Nummer, string Name, string? Organisation, string? Niederlassung, int? GruppeId);
public sealed record GruppenUebersicht(IReadOnlyList<GruppeInfo> Gruppen, IReadOnlyList<GruppenClient> Clients,
    int GruppenGesamt, int ClientsGesamt, int ClientsTreffer, bool Gekuerzt);

public enum GruppeAnlegenStatus { Ok, NameFehlt, NameZuLang, NameDoppelt }
public sealed record GruppeAnlegenResult(GruppeAnlegenStatus Status, GruppeInfo? Gruppe);

public enum ZuordnungResult { Ok, UngueltigeEingabe, GruppeNichtGefunden, MandantNichtGefunden }

public interface IMandantengruppenService
{
    Task<GruppenUebersicht> ListenAsync(string? gruppenFilter, string? clientFilter, CancellationToken ct);
    Task<GruppeAnlegenResult> AnlegenAsync(string? name, CancellationToken ct);
    // Ordnet den Mandanten der Gruppe zu; eine bestehende Zuordnung zu einer anderen Gruppe wird dabei verschoben.
    Task<ZuordnungResult> ZuordnenAsync(string? clientId, int gruppeId, CancellationToken ct);
    // Hebt die Zuordnung auf (idempotent).
    Task<ZuordnungResult> EntfernenAsync(string? clientId, CancellationToken ct);
}

public sealed class EfMandantengruppenService : IMandantengruppenService
{
    public const int FilterMaxLaenge = 100;
    public const int MaxClients = 500;

    private readonly IAppDbContextFactory _contexts;
    public EfMandantengruppenService(IAppDbContextFactory contexts) => _contexts = contexts;

    private static string? NormalisiereFilter(string? f)
    {
        f = f?.Trim();
        if (string.IsNullOrEmpty(f)) return null;
        return (f.Length > FilterMaxLaenge ? f[..FilterMaxLaenge] : f).ToLowerInvariant();
    }

    public async Task<GruppenUebersicht> ListenAsync(string? gruppenFilter, string? clientFilter, CancellationToken ct)
    {
        var gf = NormalisiereFilter(gruppenFilter);
        var cf = NormalisiereFilter(clientFilter);
        await using var db = _contexts.Create();

        var gruppenGesamt = await db.Mandantengruppen.CountAsync(ct);
        var gq = db.Mandantengruppen.AsNoTracking();
        if (gf is not null) gq = gq.Where(g => g.NameNormalisiert.ToLower().Contains(gf));
        var gruppen = await gq.OrderBy(g => g.NameNormalisiert).ThenBy(g => g.Id)
            .Select(g => new GruppeInfo(g.Id, g.Name, g.Mitglieder.Count)).ToListAsync(ct);

        var clientsGesamt = await db.MasterClients.CountAsync(ct);
        var cq = db.MasterClients.AsNoTracking();
        if (cf is not null)
            cq = cq.Where(c => c.Number.ToString().Contains(cf)
                || c.Name.ToLower().Contains(cf)
                || (c.DifferingName != null && c.DifferingName.ToLower().Contains(cf))
                || (c.OrganizationName != null && c.OrganizationName.ToLower().Contains(cf))
                || (c.OrganizationNumber != null && c.OrganizationNumber.ToString()!.Contains(cf))
                || (c.EstablishmentName != null && c.EstablishmentName.ToLower().Contains(cf))
                || (c.EstablishmentShortName != null && c.EstablishmentShortName.ToLower().Contains(cf)));
        var treffer = await cq.CountAsync(ct);

        // Nur Mandanten sichtbarer Gruppen bzw. nicht zugeordnete werden geliefert; so bleibt "Entfernen" immer möglich.
        var sichtbareGruppen = gruppen.Select(g => g.Id).ToList();
        var rows = await cq
            .Select(c => new
            {
                c.Id, c.Number, c.Name, c.OrganizationName, c.EstablishmentName,
                GruppeId = db.MandantengruppeMitglieder.Where(m => m.MasterClientId == c.Id).Select(m => (int?)m.MandantengruppeId).FirstOrDefault(),
            })
            .OrderBy(c => c.Name).ThenBy(c => c.Number)
            .Take(MaxClients + 1)
            .ToListAsync(ct);

        var clients = rows.Take(MaxClients)
            .Where(r => r.GruppeId is null || sichtbareGruppen.Contains(r.GruppeId.Value))
            .Select(r => new GruppenClient(r.Id, r.Number, r.Name, r.OrganizationName, r.EstablishmentName, r.GruppeId))
            .ToList();
        return new GruppenUebersicht(gruppen, clients, gruppenGesamt, clientsGesamt, treffer, rows.Count > MaxClients);
    }

    public async Task<GruppeAnlegenResult> AnlegenAsync(string? name, CancellationToken ct)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Any(char.IsControl)) return new(GruppeAnlegenStatus.NameFehlt, null);
        if (name.Length > Mandantengruppe.NameMaxLaenge) return new(GruppeAnlegenStatus.NameZuLang, null);
        var norm = Mandantengruppe.Normalisieren(name);

        await using var db = _contexts.Create();
        if (await db.Mandantengruppen.AnyAsync(g => g.NameNormalisiert == norm, ct)) return new(GruppeAnlegenStatus.NameDoppelt, null);
        var gruppe = new Mandantengruppe { Name = name, NameNormalisiert = norm };
        db.Mandantengruppen.Add(gruppe);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // parallele Anlage desselben Namens (Unique-Index)
            return new(GruppeAnlegenStatus.NameDoppelt, null);
        }
        return new(GruppeAnlegenStatus.Ok, new GruppeInfo(gruppe.Id, gruppe.Name, 0));
    }

    private static bool IdGueltig(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 450;

    public async Task<ZuordnungResult> ZuordnenAsync(string? clientId, int gruppeId, CancellationToken ct)
    {
        if (!IdGueltig(clientId) || gruppeId <= 0) return ZuordnungResult.UngueltigeEingabe;
        for (var versuch = 0; versuch < 2; versuch++)
        {
            await using var db = _contexts.Create();
            if (!await db.Mandantengruppen.AnyAsync(g => g.Id == gruppeId, ct)) return ZuordnungResult.GruppeNichtGefunden;
            if (!await db.MasterClients.AnyAsync(c => c.Id == clientId, ct)) return ZuordnungResult.MandantNichtGefunden;
            var row = await db.MandantengruppeMitglieder.FirstOrDefaultAsync(m => m.MasterClientId == clientId, ct);
            if (row is null) db.MandantengruppeMitglieder.Add(new MandantengruppeMitglied { MasterClientId = clientId!, MandantengruppeId = gruppeId });
            else if (row.MandantengruppeId == gruppeId) return ZuordnungResult.Ok;
            else { row.MandantengruppeId = gruppeId; row.ZugeordnetAm = DateTime.UtcNow; }
            try { await db.SaveChangesAsync(ct); return ZuordnungResult.Ok; }
            catch (DbUpdateException) when (versuch == 0) { /* parallele Zuordnung/Löschung: einmal mit frischem Stand wiederholen */ }
            catch (DbUpdateException) { return ZuordnungResult.MandantNichtGefunden; }
        }
        return ZuordnungResult.MandantNichtGefunden;
    }

    public async Task<ZuordnungResult> EntfernenAsync(string? clientId, CancellationToken ct)
    {
        if (!IdGueltig(clientId)) return ZuordnungResult.UngueltigeEingabe;
        await using var db = _contexts.Create();
        if (!await db.MasterClients.AnyAsync(c => c.Id == clientId, ct)) return ZuordnungResult.MandantNichtGefunden;
        var row = await db.MandantengruppeMitglieder.FirstOrDefaultAsync(m => m.MasterClientId == clientId, ct);
        if (row is null) return ZuordnungResult.Ok;
        db.MandantengruppeMitglieder.Remove(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { /* bereits parallel entfernt */ }
        return ZuordnungResult.Ok;
    }
}
