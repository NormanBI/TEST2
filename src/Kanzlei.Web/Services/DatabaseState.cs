namespace Kanzlei.Web.Services;

public enum DatabaseStatus { Unknown, NotConfigured, ConfigUnreadable, Unavailable, MailRequired, MailConfigUnreadable, AdminRequired, Ready }

// Hält den Konfigurations-/Verbindungszustand. Bei Fehlern wird nie die gespeicherte Konfiguration überschrieben.
public interface IDatabaseState
{
    DatabaseStatus Status { get; }
    DbConnectionSettings? Settings { get; }
    Task<DatabaseStatus> EnsureReadyAsync(CancellationToken ct);
    Task<DbTestResult> ConfigureAsync(DbConnectionSettings settings, CancellationToken ct);
    // Mailserver-Ersteinrichtung (nur im Status MailRequired): Test -> Speichern -> weiter zu Admin-Einrichtung bzw. Anmeldung.
    Task<MailTestResult> ConfigureMailAsync(MailSettings settings, CancellationToken ct);
    // Legt den ersten Administrator an (nur im Status AdminRequired) und schließt die Einrichtung ab.
    Task<BenutzerResult> CreateInitialAdministratorAsync(NeuerBenutzer admin, CancellationToken ct);
}

public sealed class DatabaseState : IDatabaseState
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    private readonly IDbSettingsStore _store;
    private readonly IDatabaseGateway _gateway;
    private readonly ILogger<DatabaseState> _logger;
    private readonly IBenutzerStore? _users;
    private readonly IMailSettingsStore? _mail;
    private readonly IMailConnectionTester? _mailTester;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTimeOffset _lastAttempt = DateTimeOffset.MinValue;
    private volatile DatabaseStatus _status = DatabaseStatus.Unknown;
    private DbConnectionSettings? _settings;

    public DatabaseState(IDbSettingsStore store, IDatabaseGateway gateway, ILogger<DatabaseState> logger, IBenutzerStore? users = null,
        IMailSettingsStore? mail = null, IMailConnectionTester? mailTester = null)
    {
        _store = store; _gateway = gateway; _logger = logger; _users = users; _mail = mail; _mailTester = mailTester;
    }

    public DatabaseStatus Status => _status;
    public DbConnectionSettings? Settings => _status is DatabaseStatus.Ready or DatabaseStatus.AdminRequired or DatabaseStatus.MailRequired ? _settings : null;

    // Nach erfolgreicher Migration: zuerst Mailserver-Konfiguration, dann (ohne aktiven Administrator) Admin-Einrichtung.
    // Eine nicht lesbare Mail-Konfiguration sperrt fail-closed, ohne sie zu ändern.
    private async Task<DatabaseStatus> ReadyOrAdminRequiredAsync(CancellationToken ct)
    {
        if (_mail is not null)
        {
            var mail = await _mail.LoadAsync(ct);
            if (mail.Status == MailSettingsLoadStatus.Missing) return DatabaseStatus.MailRequired;
            if (mail.Status == MailSettingsLoadStatus.Unreadable) return DatabaseStatus.MailConfigUnreadable;
        }
        return await AdminOrReadyAsync(ct);
    }

    private async Task<DatabaseStatus> AdminOrReadyAsync(CancellationToken ct)
    {
        if (_users is null) return DatabaseStatus.Ready;
        return await _users.HasActiveAdministratorAsync(ct) ? DatabaseStatus.Ready : DatabaseStatus.AdminRequired;
    }

    public async Task<DatabaseStatus> EnsureReadyAsync(CancellationToken ct)
    {
        var current = _status;
        if (current is DatabaseStatus.Ready or DatabaseStatus.AdminRequired or DatabaseStatus.MailRequired) return current;
        await _lock.WaitAsync(ct);
        try
        {
            if (_status is DatabaseStatus.Ready or DatabaseStatus.AdminRequired or DatabaseStatus.MailRequired) return _status;
            // Fehlende/defekte Konfiguration wird bei jeder Anfrage neu geprüft (günstig); Verbindungsfehler gedrosselt.
            if ((_status is DatabaseStatus.Unavailable or DatabaseStatus.MailConfigUnreadable) && DateTimeOffset.UtcNow - _lastAttempt < RetryInterval) return _status;
            _lastAttempt = DateTimeOffset.UtcNow;

            var load = _store.Load();
            switch (load.Status)
            {
                case DbSettingsLoadStatus.Missing:
                    _settings = null; _status = DatabaseStatus.NotConfigured; break;
                case DbSettingsLoadStatus.Unreadable:
                    _settings = null; _status = DatabaseStatus.ConfigUnreadable; break;
                default:
                    var result = await _gateway.MigrateAsync(load.Settings!, null, ct);
                    if (result.Success)
                    {
                        _settings = load.Settings;
                        try { _status = await ReadyOrAdminRequiredAsync(ct); }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _status = DatabaseStatus.Unavailable;
                            _logger.LogWarning("Prüfung der Einrichtung fehlgeschlagen ({ExceptionType}).", ex.GetType().Name);
                            break;
                        }
                        _logger.LogInformation("Datenbank bereit (Migrationen angewendet).");
                    }
                    else
                    {
                        _status = DatabaseStatus.Unavailable;
                        _logger.LogWarning("Datenbank nicht verfügbar: {Outcome}.", result.Outcome);
                    }
                    break;
            }
            return _status;
        }
        finally { _lock.Release(); }
    }

    // Test -> Migration -> erst dann speichern. Bei Fehlern wird nichts persistiert.
    public async Task<DbTestResult> ConfigureAsync(DbConnectionSettings settings, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_status is not (DatabaseStatus.NotConfigured or DatabaseStatus.Unknown))
                return new DbTestResult(DbTestOutcome.Other);
            var test = await _gateway.TestConnectionAsync(settings, ct);
            if (!test.Success) return test;
            var migrated = await _gateway.MigrateAsync(settings, test.ServerVersion, ct);
            if (!migrated.Success) return migrated;
            _store.Save(settings);
            _settings = settings;
            try { _status = await ReadyOrAdminRequiredAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Konfiguration ist gespeichert; Administratorprüfung wird beim nächsten Zugriff wiederholt.
                _status = DatabaseStatus.Unavailable;
                _logger.LogWarning("Prüfung der Einrichtung fehlgeschlagen ({ExceptionType}).", ex.GetType().Name);
                return new DbTestResult(DbTestOutcome.Other);
            }
            _logger.LogInformation("Datenbankverbindung konfiguriert und gespeichert.");
            return test;
        }
        finally { _lock.Release(); }
    }

    public async Task<MailTestResult> ConfigureMailAsync(MailSettings settings, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_status is not DatabaseStatus.MailRequired || _mail is null || _mailTester is null) return new MailTestResult(MailTestOutcome.Other);
            if (!settings.IsValid()) return new MailTestResult(MailTestOutcome.Other);
            var test = await _mailTester.TestAsync(settings, ct);
            if (!test.Success) return test;
            try
            {
                if (await _mail.SaveAsync(settings, ct) != MailSaveResult.Ok) return new MailTestResult(MailTestOutcome.StorageFailed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Mailserver-Einstellungen konnten nicht gespeichert werden ({ExceptionType}).", ex.GetType().Name);
                return new MailTestResult(MailTestOutcome.StorageFailed);
            }
            try { _status = await AdminOrReadyAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Gespeichert; die Folgeprüfung wird beim nächsten Zugriff wiederholt.
                _status = DatabaseStatus.Unavailable;
                _logger.LogWarning("Prüfung der Einrichtung fehlgeschlagen ({ExceptionType}).", ex.GetType().Name);
            }
            _logger.LogInformation("Mailserver-Einstellungen gespeichert.");
            return test;
        }
        finally { _lock.Release(); }
    }

    public async Task<BenutzerResult> CreateInitialAdministratorAsync(NeuerBenutzer admin, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_status is not DatabaseStatus.AdminRequired || _users is null) return BenutzerResult.AdministratorExists;
            var result = await _users.CreateInitialAdministratorAsync(admin, ct);
            if (result is BenutzerResult.Ok or BenutzerResult.AdministratorExists)
            {
                _status = DatabaseStatus.Ready;
                if (result == BenutzerResult.Ok) _logger.LogInformation("Initialer Administrator angelegt.");
            }
            return result;
        }
        finally { _lock.Release(); }
    }
}
