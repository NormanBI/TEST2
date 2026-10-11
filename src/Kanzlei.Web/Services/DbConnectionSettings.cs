using System.Text.RegularExpressions;
using MySqlConnector;

namespace Kanzlei.Web.Services;

public enum DbSslMode { None, Preferred, Required }

// ToString() gibt bewusst weder Passwort noch Verbindungszeichenfolge aus.
public sealed record DbConnectionSettings(string Host, int Port, string Database, string UserName, string Password, DbSslMode SslMode)
{
    public const int DefaultPort = 3306;
    public const int ConnectTimeoutSeconds = 5;

    private static readonly Regex HostPattern = new(@"^[A-Za-z0-9._:\-\[\]]{1,255}$", RegexOptions.Compiled);
    private static readonly Regex DatabasePattern = new(@"^[A-Za-z0-9_$\-]{1,64}$", RegexOptions.Compiled);

    public static bool IsValidHost(string? v) => v is not null && HostPattern.IsMatch(v);
    public static bool IsValidPort(int v) => v is >= 1 and <= 65535;
    public static bool IsValidDatabase(string? v) => v is not null && DatabasePattern.IsMatch(v);
    public static bool IsValidUser(string? v) => !string.IsNullOrWhiteSpace(v) && v.Length <= 80 && !v.Any(char.IsControl);
    public static bool IsValidPassword(string? v) => !string.IsNullOrEmpty(v) && v.Length <= 256 && !v.Any(char.IsControl);

    public bool IsValid() => IsValidHost(Host) && IsValidPort(Port) && IsValidDatabase(Database)
        && IsValidUser(UserName) && IsValidPassword(Password) && Enum.IsDefined(SslMode);

    // Wird nur im Speicher verwendet; Werte werden vom Builder korrekt escaped (kein Injection über Eingabefelder).
    public string BuildConnectionString() => new MySqlConnectionStringBuilder
    {
        Server = Host,
        Port = (uint)Port,
        Database = Database,
        UserID = UserName,
        Password = Password,
        SslMode = SslMode switch
        {
            DbSslMode.Required => MySqlSslMode.Required,
            DbSslMode.Preferred => MySqlSslMode.Preferred,
            _ => MySqlSslMode.None,
        },
        ConnectionTimeout = ConnectTimeoutSeconds,
        DefaultCommandTimeout = 30,
        Pooling = true,
    }.ConnectionString;

    public override string ToString() => $"{Host}:{Port}/{Database}";
}
