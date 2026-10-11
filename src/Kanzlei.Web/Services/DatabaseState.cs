namespace Kanzlei.Web.Services;

public enum DatabaseStatus { Unknown, NotConfigured, ConfigUnreadable, Unavailable, Ready }

// Hält den Konfigurations-/Verbindungszustand. Bei Fehlern wird nie die gespeicherte Konfiguration überschrieben.
public interface IDatabaseState
{
    DatabaseStatus Status { get; }
    DbConnectionSettings? Settings { get; }
    Task<DatabaseStatus> EnsureReadyAsync(CancellationToken ct);
    Task<DbTestResult> ConfigureAsync(DbConnectionSettings settings, CancellationToken ct);
}

public sealed class DatabaseState : IDatabaseState
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    private readonly IDbSettingsStore _store;
    private readonly IDatabaseGateway _gateway;
    private readonly ILogger<DatabaseState> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTimeOffset _lastAttempt = DateTimeOffset.MinValue;
    private volatile DatabaseStatus _status = DatabaseStatus.Unknown;
    private DbConnectionSettings? _settings;

    public DatabaseState(IDbSettingsStore store, IDatabaseGateway gateway, ILogger<DatabaseState> logger)
    {
        _store = store; _gateway = gateway; _logger = logger;
    }

    public DatabaseStatus Status => _status;
    public DbConnectionSettings? Settings => _status == DatabaseStatus.Ready ? _settings : null;

    public async Task<DatabaseStatus> EnsureReadyAsync(CancellationToken ct)
    {
        var current = _status;
        if (current is DatabaseStatus.Ready) return current;
        await _lock.WaitAsync(ct);
        try
        {
            if (_status is DatabaseStatus.Ready) return _status;
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
                        _settings = load.Settings; _status = DatabaseStatus.Ready;
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
            _settings = settings; _status = DatabaseStatus.Ready;
            _logger.LogInformation("Datenbankverbindung konfiguriert und gespeichert.");
            return test;
        }
        finally { _lock.Release(); }
    }
}
