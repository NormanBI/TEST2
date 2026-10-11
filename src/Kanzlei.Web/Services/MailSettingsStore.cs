using System.Security.Cryptography;
using Kanzlei.Web.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Kanzlei.Web.Services;

// Unreadable: Datensatz vorhanden, aber ungültig oder Passwort nicht entschlüsselbar (z. B. Schlüsselbund fehlt). Wird nie überschrieben.
public enum MailSettingsLoadStatus { Missing, Ok, Unreadable }

public sealed record MailSettingsLoadResult(MailSettingsLoadStatus Status, MailSettings? Settings);

public enum MailSaveResult { Ok, Unreadable, PasswordRequired }

public interface IMailSettingsStore
{
    Task<MailSettingsLoadResult> LoadAsync(CancellationToken ct);
    // Password == null behält das gespeicherte Passwort (nur für Bearbeitung); bei nicht entschlüsselbarem Bestand wird nichts geschrieben.
    Task<MailSaveResult> SaveAsync(MailSettings settings, CancellationToken ct);
}

public sealed class EfMailSettingsStore : IMailSettingsStore
{
    public const string Purpose = "Kanzlei.Web.MailSettings.Password.v1";

    private readonly IAppDbContextFactory _contexts;
    private readonly IDataProtector _protector;
    private readonly ILogger<EfMailSettingsStore>? _logger;

    public EfMailSettingsStore(IAppDbContextFactory contexts, IDataProtectionProvider provider, ILogger<EfMailSettingsStore>? logger = null)
    {
        _contexts = contexts;
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    public async Task<MailSettingsLoadResult> LoadAsync(CancellationToken ct)
    {
        using var db = _contexts.Create();
        var row = await db.MailEinstellungen.AsNoTracking().FirstOrDefaultAsync(m => m.Id == MailEinstellungen.SingletonId, ct);
        if (row is null || !row.Konfiguriert) return new(MailSettingsLoadStatus.Missing, null);
        return Map(row);
    }

    private MailSettingsLoadResult Unreadable(string reason)
    {
        _logger?.LogError("Mailserver-Einstellungen nicht lesbar ({Reason}).", reason);
        return new(MailSettingsLoadStatus.Unreadable, null);
    }

    private MailSettingsLoadResult Map(MailEinstellungen row)
    {
        string? password = null;
        try
        {
            if (!string.IsNullOrEmpty(row.PasswortVerschluesselt)) password = _protector.Unprotect(row.PasswortVerschluesselt);
        }
        catch (CryptographicException ex)
        {
            return Unreadable(ex.GetType().Name);
        }
        var settings = new MailSettings(row.SmtpHost, row.SmtpPort, string.IsNullOrEmpty(row.Benutzername) ? null : row.Benutzername,
            password, row.AbsenderEmail, row.AbsenderName, row.SslModus);
        return settings.IsValid() ? new(MailSettingsLoadStatus.Ok, settings) : Unreadable("ungültige Werte");
    }

    public async Task<MailSaveResult> SaveAsync(MailSettings s, CancellationToken ct)
    {
        using var db = _contexts.Create();
        var row = await db.MailEinstellungen.FirstOrDefaultAsync(m => m.Id == MailEinstellungen.SingletonId, ct);
        var isNew = row is null;
        row ??= new MailEinstellungen { Id = MailEinstellungen.SingletonId };

        string? cipher;
        if (!s.HasCredentials) cipher = null;
        else if (s.Password is not null) cipher = _protector.Protect(s.Password);
        else
        {
            // Passwort beibehalten: nur möglich, wenn das gespeicherte Chiffrat vorhanden und entschlüsselbar ist.
            if (string.IsNullOrEmpty(row.PasswortVerschluesselt)) return MailSaveResult.PasswordRequired;
            try { _protector.Unprotect(row.PasswortVerschluesselt); }
            catch (CryptographicException) { return MailSaveResult.Unreadable; }
            cipher = row.PasswortVerschluesselt;
        }

        var now = DateTime.UtcNow;
        row.SmtpHost = s.Host; row.SmtpPort = s.Port;
        row.Benutzername = s.HasCredentials ? s.UserName : null;
        row.PasswortVerschluesselt = cipher;
        row.AbsenderEmail = s.SenderEmail; row.AbsenderName = s.SenderName; row.SslModus = s.Tls;
        row.Aktiv = true; row.Konfiguriert = true; row.LetzterTestAm = now; row.GeaendertAm = now;
        if (isNew) { row.ErstelltAm = now; db.MailEinstellungen.Add(row); }
        await db.SaveChangesAsync(ct);
        return MailSaveResult.Ok;
    }
}
