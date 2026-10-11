using Kanzlei.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Kanzlei.Web.Services;

public enum BrandingSaveResult { Ok, Invalid, StorageFailed }

public interface IBrandingService
{
    // Liefert den Navigationstext aus dem Cache; bei fehlendem Datensatz oder Datenbankfehler den Standardtext (nie eine Ausnahme).
    Task<string> GetBrandTextAsync(CancellationToken ct = default);
    // Legt den Datensatz mit dem Standardtext an, falls er fehlt (idempotent).
    Task EnsureRowAsync(CancellationToken ct);
    Task<BrandingSaveResult> SaveAsync(string? text, CancellationToken ct);
}

public static class BrandingValidation
{
    // Pflicht, getrimmt, max. 80 Zeichen, keine Steuerzeichen und kein Markup (< > &).
    public static bool TryNormalize(string? input, out string text, out string? error)
    {
        text = (input ?? "").Trim();
        error = null;
        if (text.Length == 0) error = "Bitte einen Text für die Navigation angeben.";
        else if (text.Length > Einstellungen.NavigationstextMaxLaenge)
            error = $"Der Text darf höchstens {Einstellungen.NavigationstextMaxLaenge} Zeichen lang sein.";
        else if (text.Any(char.IsControl)) error = "Der Text darf keine Steuerzeichen enthalten.";
        else if (text.IndexOfAny(new[] { '<', '>', '&' }) >= 0) error = "Der Text darf kein HTML/Markup (< > &) enthalten.";
        return error is null;
    }
}

public sealed class EfBrandingService : IBrandingService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(10);

    private readonly IAppDbContextFactory _contexts;
    private readonly ILogger<EfBrandingService>? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile string? _cached;
    private DateTimeOffset _lastFailure = DateTimeOffset.MinValue;

    public EfBrandingService(IAppDbContextFactory contexts, ILogger<EfBrandingService>? logger = null)
    {
        _contexts = contexts; _logger = logger;
    }

    public async Task<string> GetBrandTextAsync(CancellationToken ct = default)
    {
        var cached = _cached;
        if (cached is not null) return cached;
        if (DateTimeOffset.UtcNow - _lastFailure < RetryInterval) return Einstellungen.StandardNavigationstext;
        await _lock.WaitAsync(ct);
        try
        {
            if (_cached is not null) return _cached;
            if (DateTimeOffset.UtcNow - _lastFailure < RetryInterval) return Einstellungen.StandardNavigationstext;
            try
            {
                using var db = _contexts.Create();
                var row = await db.Einstellungen.AsNoTracking().FirstOrDefaultAsync(e => e.Id == Einstellungen.SingletonId, ct);
                // Fehlender oder ungültiger Datensatz: Standardtext, aber nicht dauerhaft gecacht (Datensatz kann später entstehen).
                if (row is not null && BrandingValidation.TryNormalize(row.Navigationstext, out var text, out _))
                {
                    _cached = text;
                    return text;
                }
                if (row is not null) _logger?.LogWarning("Gespeicherter Navigationstext ist ungültig; Standardtext wird verwendet.");
                _lastFailure = DateTimeOffset.UtcNow;
                return Einstellungen.StandardNavigationstext;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _lastFailure = DateTimeOffset.UtcNow;
                _logger?.LogWarning("Navigationstext konnte nicht geladen werden ({ExceptionType}); Standardtext wird verwendet.", ex.GetType().Name);
                return Einstellungen.StandardNavigationstext;
            }
        }
        finally { _lock.Release(); }
    }

    public async Task EnsureRowAsync(CancellationToken ct)
    {
        using var db = _contexts.Create();
        if (await db.Einstellungen.AnyAsync(e => e.Id == Einstellungen.SingletonId, ct)) return;
        db.Einstellungen.Add(new Einstellungen());
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Parallel angelegt (Primärschlüssel-Konflikt): gewünschter Zustand ist erreicht.
            using var check = _contexts.Create();
            if (!await check.Einstellungen.AnyAsync(e => e.Id == Einstellungen.SingletonId, ct)) throw;
        }
    }

    public async Task<BrandingSaveResult> SaveAsync(string? text, CancellationToken ct)
    {
        if (!BrandingValidation.TryNormalize(text, out var normalized, out _)) return BrandingSaveResult.Invalid;
        try
        {
            using var db = _contexts.Create();
            var row = await db.Einstellungen.FirstOrDefaultAsync(e => e.Id == Einstellungen.SingletonId, ct);
            if (row is null)
                db.Einstellungen.Add(new Einstellungen { Navigationstext = normalized });
            else
            {
                row.Navigationstext = normalized;
                row.GeaendertAm = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            _cached = normalized;
            return BrandingSaveResult.Ok;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning("Navigationstext konnte nicht gespeichert werden ({ExceptionType}).", ex.GetType().Name);
            return BrandingSaveResult.StorageFailed;
        }
    }
}
