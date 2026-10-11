namespace Kanzlei.Web.Services;

public enum DatabaseStatus { Unknown, NotConfigured, ConfigUnreadable, Unavailable, AdminRequired, Ready }

// Hält den Konfigurations-/Verbindungszustand. Bei Fehlern wird nie die gespeicherte Konfiguration überschrieben.
public interface IDatabaseState
{
    DatabaseStatus Status { get; }
    DbConnectionSettings? Settings { get; }
    Task<DatabaseStatus> EnsureReadyAsync(CancellationToken ct);
    Task<DbTestResult> ConfigureAsync(DbConnectionSettings settings, CancellationToken ct);
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
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTimeOffset _lastAttempt = DateTimeOffset.MinValue;
    private volatile DatabaseStatus _status = DatabaseStatus.Unknown;
    private DbConnectionSettings? _settings;

    public DatabaseState(IDbSettingsStore store, IDatabaseGateway gateway, ILogger<DatabaseState> logger, IBenutzerStore? users = null)
    {
        _store = store; _gateway = gateway; _logger = logger; _users = users;
    }

    public DatabaseStatus Status => _status;
    public DbConnectionSettings? Settings => _status is DatabaseStatus.Ready or DatabaseStatus.AdminRequired ? _settings : null;

    // Nach erfolgreicher Migration: Ohne aktiven Administrator ist zuerst die Admin-Einrichtung nötig.
    private async Task<DatabaseStatus> ReadyOrAdminRequiredAsync(CancellationToken ct)
    {
        if (_users is null) return DatabaseStatus.Ready;
        return await _users.HasActiveAdministratorAsync(ct) ? DatabaseStatus.Ready : DatabaseStatus.AdminRequired;
    }

    public async Task<DatabaseStatus> EnsureReadyAsync(CancellationToken ct)
    {
        var current = _status;
        if (current is DatabaseStatus.Ready or DatabaseStatus.AdminRequired) return current;
        await _lock.WaitAsync(ct);
        try
        {
            if (_status is DatabaseStatus.Ready or DatabaseStatus.AdminRequired) return _status;
            // Fehlende/defekte Konfiguration wird bei jeder Anfrage neu geprüft (günstig); Verbindungsfehler gedrosselt.
            if (_status is DatabaseStatus.Unavailable && DateTimeOffset.UtcNow - _lastAttempt < RetryInterval) return _status;
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
                            _logger.LogWarning("Prüfung auf Administrator fehlgeschlagen ({ExceptionType}).", ex.GetType().Name);
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
                _logger.LogWarning("Prüfung auf Administrator fehlgeschlagen ({ExceptionType}).", ex.GetType().Name);
                return new DbTestResult(DbTestOutcome.Other);
            }
            _logger.LogInformation("Datenbankverbindung konfiguriert und gespeichert.");
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
