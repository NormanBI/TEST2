using Microsoft.EntityFrameworkCore;

namespace Kanzlei.Web.Services;

// Eine Kategorie mit Anzahl. Schluessel "" steht für fehlende/leere Werte; Bezeichnung ist der deutsche Anzeigetext.
public sealed record KategorieAnzahl(string Schluessel, string Bezeichnung, int Anzahl);

public sealed record MandantenAuswertung(IReadOnlyList<KategorieAnzahl> Status, IReadOnlyList<KategorieAnzahl> Typ);

public interface IMandantenAuswertungService
{
    // statusFilter/typFilter: null = kein Filter, "" = Kategorie "Ohne ...", sonst der Kategoriewert.
    // Das Statusdiagramm berücksichtigt nur den Typfilter, das Typdiagramm nur den Statusfilter.
    // Unbekannte Filterwerte führen zu null (Aufrufer antwortet mit 400).
    Task<MandantenAuswertung?> BerechnenAsync(string? statusFilter, string? typFilter, CancellationToken ct);
}

public sealed class EfMandantenAuswertungService : IMandantenAuswertungService
{
    public const string OhneStatus = "Ohne Status";
    public const string OhneTyp = "Ohne Typ";

    private readonly IAppDbContextFactory _contexts;
    public EfMandantenAuswertungService(IAppDbContextFactory contexts) => _contexts = contexts;

    public async Task<MandantenAuswertung?> BerechnenAsync(string? statusFilter, string? typFilter, CancellationToken ct)
    {
        await using var db = _contexts.Create();
        var clients = db.MasterClients.AsNoTracking();

        // Normalisierung (null/leer/Leerraum -> "") und Gruppierung laufen serverseitig; es werden nur Kategorie und Anzahl geladen.
        var statusGroups = await clients
            .Select(c => new { S = c.Status == null || c.Status.Trim() == "" ? "" : c.Status.Trim(), T = c.Type == null || c.Type.Trim() == "" ? "" : c.Type.Trim() })
            .GroupBy(x => new { x.S, x.T })
            .Select(g => new { g.Key.S, g.Key.T, N = g.Count() })
            .ToListAsync(ct);

        var bekannteStatus = statusGroups.Select(g => g.S).ToHashSet(StringComparer.Ordinal);
        var bekannteTypen = statusGroups.Select(g => g.T).ToHashSet(StringComparer.Ordinal);
        if (statusFilter is not null && !bekannteStatus.Contains(statusFilter)) return null;
        if (typFilter is not null && !bekannteTypen.Contains(typFilter)) return null;

        var status = Gruppieren(statusGroups.Where(g => typFilter is null || g.T == typFilter), g => g.S, g => g.N, OhneStatus);
        var typ = Gruppieren(statusGroups.Where(g => statusFilter is null || g.S == statusFilter), g => g.T, g => g.N, OhneTyp);
        return new MandantenAuswertung(status, typ);
    }

    // Deterministische Reihenfolge: Anzahl absteigend, dann Schlüssel ordinal aufsteigend.
    private static IReadOnlyList<KategorieAnzahl> Gruppieren<T>(IEnumerable<T> rows, Func<T, string> key, Func<T, int> count, string ohne) =>
        rows.GroupBy(key, StringComparer.Ordinal)
            .Select(g => new KategorieAnzahl(g.Key, g.Key.Length == 0 ? ohne : g.Key, g.Sum(count)))
            .OrderByDescending(k => k.Anzahl).ThenBy(k => k.Schluessel, StringComparer.Ordinal)
            .ToList();
}
