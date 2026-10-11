using Kanzlei.Web.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Kanzlei.Web.Services;

public enum DbTestOutcome { Ok, Unreachable, InvalidCredentials, InvalidDatabase, Timeout, Other }

public sealed record DbTestResult(DbTestOutcome Outcome, string? ServerVersion = null)
{
    public bool Success => Outcome == DbTestOutcome.Ok;

    // Sichere deutsche Meldung ohne Zugangsdaten oder Verbindungszeichenfolge.
    public string Message => Outcome switch
    {
        DbTestOutcome.Ok => "Verbindung erfolgreich.",
        DbTestOutcome.Unreachable => "Der Datenbankserver ist nicht erreichbar. Bitte Host und Port prüfen.",
        DbTestOutcome.InvalidCredentials => "Anmeldung am Datenbankserver fehlgeschlagen. Bitte Benutzername und Passwort prüfen.",
        DbTestOutcome.InvalidDatabase => "Die Datenbank existiert nicht oder der Benutzer hat keinen Zugriff darauf.",
        DbTestOutcome.Timeout => "Zeitüberschreitung beim Verbindungsaufbau. Bitte Host, Port und Netzwerk prüfen.",
        _ => "Die Verbindung konnte nicht hergestellt werden.",
    };
}

public interface IDatabaseGateway
{
    Task<DbTestResult> TestConnectionAsync(DbConnectionSettings settings, CancellationToken ct);
    // Wendet ausstehende EF-Core-Migrationen an.
    Task<DbTestResult> MigrateAsync(DbConnectionSettings settings, string? serverVersion, CancellationToken ct);
}

public sealed class MySqlDatabaseGateway : IDatabaseGateway
{
    private readonly ILogger<MySqlDatabaseGateway> _logger;
    public MySqlDatabaseGateway(ILogger<MySqlDatabaseGateway> logger) => _logger = logger;

    public async Task<DbTestResult> TestConnectionAsync(DbConnectionSettings settings, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(DbConnectionSettings.ConnectTimeoutSeconds + 3));
            await using var conn = new MySqlConnection(settings.BuildConnectionString());
            await conn.OpenAsync(cts.Token);
            await using var cmd = new MySqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(cts.Token);
            return new DbTestResult(DbTestOutcome.Ok, conn.ServerVersion);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Map(ex, ct);
        }
    }

    public async Task<DbTestResult> MigrateAsync(DbConnectionSettings settings, string? serverVersion, CancellationToken ct)
    {
        try
        {
            if (serverVersion is null)
            {
                var test = await TestConnectionAsync(settings, ct);
                if (!test.Success) return test;
                serverVersion = test.ServerVersion;
            }
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(settings.BuildConnectionString(), ServerVersion.Parse(serverVersion!))
                .Options;
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync(ct);
            return new DbTestResult(DbTestOutcome.Ok, serverVersion);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Map(ex, ct);
        }
    }

    private DbTestResult Map(Exception ex, CancellationToken ct)
    {
        var root = ex;
        while (root.InnerException is not null && root is not MySqlException) root = root.InnerException;
        var outcome = root switch
        {
            MySqlException m when m.ErrorCode == MySqlErrorCode.AccessDenied => DbTestOutcome.InvalidCredentials,
            MySqlException m when m.ErrorCode == MySqlErrorCode.UnknownDatabase || m.ErrorCode == MySqlErrorCode.DatabaseAccessDenied => DbTestOutcome.InvalidDatabase,
            MySqlException m when m.ErrorCode == MySqlErrorCode.UnableToConnectToHost => DbTestOutcome.Unreachable,
            MySqlException m when m.InnerException is TimeoutException || m.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => DbTestOutcome.Timeout,
            MySqlException => DbTestOutcome.Other,
            TimeoutException => DbTestOutcome.Timeout,
            OperationCanceledException when !ct.IsCancellationRequested => DbTestOutcome.Timeout,
            System.Net.Sockets.SocketException => DbTestOutcome.Unreachable,
            _ => DbTestOutcome.Other,
        };
        // Meldungen können Hostnamen enthalten; deshalb nur Typ und Fehlercode protokollieren.
        _logger.LogWarning("Datenbankzugriff fehlgeschlagen: {Outcome} ({ExceptionType}, Code {Code}).",
            outcome, root.GetType().Name, (root as MySqlException)?.ErrorCode);
        return new DbTestResult(outcome);
    }
}
