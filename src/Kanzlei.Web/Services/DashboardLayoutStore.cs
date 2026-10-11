using System.Text.Json;
using Kanzlei.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Kanzlei.Web.Services;

public sealed record PanelLayout(string Id, int Row, int Col, int SizeX, int SizeY);

public enum LayoutSaveResult { Ok, Invalid, UserNotFound }

public interface IDashboardLayoutStore
{
    // Liefert das gespeicherte Layout des Benutzers; fehlend oder ungültig -> Standardlayout.
    Task<IReadOnlyList<PanelLayout>> LoadAsync(int benutzerId, CancellationToken ct);
    Task<LayoutSaveResult> SaveAsync(int benutzerId, IReadOnlyList<PanelLayout>? panels, CancellationToken ct);
    // Entfernt das gespeicherte Layout (zurück zum Standard).
    Task ResetAsync(int benutzerId, CancellationToken ct);
}

public static class MandantenLayout
{
    public const string StatusPanel = "panel-status";
    public const string TypPanel = "panel-typ";
    public const int Spalten = 12;
    public const int MaxZeile = 50;
    public const int MaxHoehe = 12;

    public static readonly IReadOnlyList<string> BekanntePanels = new[] { StatusPanel, TypPanel };

    public static IReadOnlyList<PanelLayout> Standard { get; } = new[]
    {
        new PanelLayout(StatusPanel, 0, 0, 6, 5),
        new PanelLayout(TypPanel, 0, 6, 6, 5),
    };

    // Genau die bekannten Panels je einmal, ganzzahlige Werte in festen Grenzen, innerhalb des Rasters und überlappungsfrei.
    public static bool IstGueltig(IReadOnlyList<PanelLayout>? panels)
    {
        if (panels is null || panels.Count != BekanntePanels.Count) return false;
        if (!BekanntePanels.All(id => panels.Count(p => p is not null && p.Id == id) == 1)) return false;
        foreach (var p in panels)
        {
            if (p.Row < 0 || p.Row > MaxZeile || p.Col < 0 || p.Col >= Spalten) return false;
            if (p.SizeX < 1 || p.SizeX > Spalten || p.Col + p.SizeX > Spalten) return false;
            if (p.SizeY < 1 || p.SizeY > MaxHoehe) return false;
        }
        var a = panels[0]; var b = panels[1];
        var ueberlappt = a.Col < b.Col + b.SizeX && b.Col < a.Col + a.SizeX && a.Row < b.Row + b.SizeY && b.Row < a.Row + a.SizeY;
        return !ueberlappt;
    }

    public static IReadOnlyList<PanelLayout>? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > BenutzerDashboardLayout.LayoutJsonMaxLaenge) return null;
        try
        {
            var panels = JsonSerializer.Deserialize<List<PanelLayout>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return IstGueltig(panels) ? panels : null;
        }
        catch (JsonException) { return null; }
    }

    // Kanonische Serialisierung ausschließlich aus validierten Werten.
    public static string Serialize(IReadOnlyList<PanelLayout> panels) =>
        JsonSerializer.Serialize(panels.OrderBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => new PanelLayout(p.Id, p.Row, p.Col, p.SizeX, p.SizeY)));
}

public sealed class EfDashboardLayoutStore : IDashboardLayoutStore
{
    private readonly IAppDbContextFactory _contexts;
    public EfDashboardLayoutStore(IAppDbContextFactory contexts) => _contexts = contexts;

    public async Task<IReadOnlyList<PanelLayout>> LoadAsync(int benutzerId, CancellationToken ct)
    {
        await using var db = _contexts.Create();
        var json = await db.BenutzerDashboardLayouts.AsNoTracking()
            .Where(l => l.BenutzerId == benutzerId).Select(l => l.LayoutJson).FirstOrDefaultAsync(ct);
        return MandantenLayout.TryParse(json) ?? MandantenLayout.Standard;
    }

    public async Task<LayoutSaveResult> SaveAsync(int benutzerId, IReadOnlyList<PanelLayout>? panels, CancellationToken ct)
    {
        if (!MandantenLayout.IstGueltig(panels)) return LayoutSaveResult.Invalid;
        var json = MandantenLayout.Serialize(panels!);
        for (var versuch = 0; ; versuch++)
        {
            await using var db = _contexts.Create();
            if (!await db.Benutzer.AnyAsync(b => b.Id == benutzerId, ct)) return LayoutSaveResult.UserNotFound;
            var row = await db.BenutzerDashboardLayouts.FirstOrDefaultAsync(l => l.BenutzerId == benutzerId, ct);
            if (row is null) db.BenutzerDashboardLayouts.Add(new BenutzerDashboardLayout { BenutzerId = benutzerId, LayoutJson = json });
            else { row.LayoutJson = json; row.GeaendertAm = DateTime.UtcNow; }
            try { await db.SaveChangesAsync(ct); return LayoutSaveResult.Ok; }
            catch (DbUpdateException) when (versuch == 0) { /* parallele Erstanlage (Unique-Index): einmal erneut als Update versuchen */ }
        }
    }

    public async Task ResetAsync(int benutzerId, CancellationToken ct)
    {
        await using var db = _contexts.Create();
        var rows = await db.BenutzerDashboardLayouts.Where(l => l.BenutzerId == benutzerId).ToListAsync(ct);
        if (rows.Count == 0) return;
        db.BenutzerDashboardLayouts.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
