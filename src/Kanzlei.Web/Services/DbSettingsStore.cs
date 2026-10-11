using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Kanzlei.Web.Services;

public enum DbSettingsLoadStatus { Missing, Ok, Unreadable }

public sealed record DbSettingsLoadResult(DbSettingsLoadStatus Status, DbConnectionSettings? Settings);

public interface IDbSettingsStore
{
    DbSettingsLoadResult Load();
    void Save(DbConnectionSettings settings);
}

// Speichert die Verbindungsparameter als JSON außerhalb von wwwroot. Nur das Passwort ist mit ASP.NET Core Data Protection
// verschlüsselt; Host, Port, Datenbank und Benutzer stehen im Klartext. Die Verbindungszeichenfolge wird nie gespeichert.
public sealed class DbSettingsStore : IDbSettingsStore
{
    public const string Purpose = "Kanzlei.Web.DbSettings.Password.v1";

    private readonly string _path;
    private readonly IDataProtector _protector;
    private readonly ILogger<DbSettingsStore>? _logger;

    public DbSettingsStore(string path, IDataProtectionProvider provider, ILogger<DbSettingsStore>? logger = null)
    {
        _path = path;
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    private sealed class FileModel
    {
        public int Version { get; set; } = 1;
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? Database { get; set; }
        public string? UserName { get; set; }
        public string? SslMode { get; set; }
        public string? ProtectedPassword { get; set; }
    }

    public DbSettingsLoadResult Load()
    {
        if (!File.Exists(_path)) return new(DbSettingsLoadStatus.Missing, null);
        try
        {
            var model = JsonSerializer.Deserialize<FileModel>(File.ReadAllText(_path));
            if (model is null || string.IsNullOrEmpty(model.ProtectedPassword)
                || !Enum.TryParse<DbSslMode>(model.SslMode, out var ssl))
            {
                _logger?.LogError("Datenbank-Konfigurationsdatei ist unvollständig.");
                return new(DbSettingsLoadStatus.Unreadable, null);
            }
            var settings = new DbConnectionSettings(model.Host ?? "", model.Port, model.Database ?? "",
                model.UserName ?? "", _protector.Unprotect(model.ProtectedPassword), ssl);
            if (!settings.IsValid())
            {
                _logger?.LogError("Datenbank-Konfigurationsdatei enthält ungültige Werte.");
                return new(DbSettingsLoadStatus.Unreadable, null);
            }
            return new(DbSettingsLoadStatus.Ok, settings);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException or UnauthorizedAccessException)
        {
            // Nur der Ausnahmetyp wird protokolliert, keine Meldung/Werte.
            _logger?.LogError("Datenbank-Konfiguration nicht lesbar ({ExceptionType}).", ex.GetType().Name);
            return new(DbSettingsLoadStatus.Unreadable, null);
        }
    }

    public void Save(DbConnectionSettings s)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        var model = new FileModel
        {
            Host = s.Host, Port = s.Port, Database = s.Database, UserName = s.UserName,
            SslMode = s.SslMode.ToString(), ProtectedPassword = _protector.Protect(s.Password),
        };
        var tmp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(tmp, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
