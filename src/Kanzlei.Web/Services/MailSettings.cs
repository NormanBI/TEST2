using Kanzlei.Web.Data;

namespace Kanzlei.Web.Services;

// ToString() gibt bewusst weder Passwort noch Benutzername aus.
public sealed record MailSettings(string Host, int Port, string? UserName, string? Password, string SenderEmail, string SenderName, MailSslModus Tls)
{
    public static bool IsValidHost(string? v) => DbConnectionSettings.IsValidHost(v);
    public static bool IsValidPort(int v) => v is >= 1 and <= 65535;
    public static bool IsValidUser(string? v) => v is null || (v.Length <= 255 && !v.Any(char.IsControl));
    public static bool IsValidPassword(string? v) => v is null || (v.Length <= 256 && !v.Any(char.IsControl));
    public static bool IsValidSenderName(string? v) => !string.IsNullOrWhiteSpace(v) && v.Length <= 100 && !v.Any(char.IsControl);
    public static bool IsValidEmail(string? v) =>
        !string.IsNullOrWhiteSpace(v) && v.Length <= 254 && !v.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))
        && System.Net.Mail.MailAddress.TryCreate(v, out var a) && a.Address == v;

    public bool HasCredentials => !string.IsNullOrEmpty(UserName);

    // Zugangsdaten dürfen nie unverschlüsselt übertragen werden.
    public bool IsValid() => IsValidHost(Host) && IsValidPort(Port) && IsValidUser(UserName) && IsValidPassword(Password)
        && IsValidEmail(SenderEmail) && IsValidSenderName(SenderName) && Enum.IsDefined(Tls)
        && (!HasCredentials || (Tls != MailSslModus.Keine && !string.IsNullOrEmpty(Password)));

    public override string ToString() => $"{Host}:{Port}";
}

public enum MailTestOutcome { Ok, AuthenticationFailed, ConnectionFailed, TlsFailed, Timeout, Other, StorageFailed }

public sealed record MailTestResult(MailTestOutcome Outcome)
{
    public bool Success => Outcome == MailTestOutcome.Ok;

    public string Message => Outcome switch
    {
        MailTestOutcome.Ok => "Verbindung zum Mailserver erfolgreich.",
        MailTestOutcome.AuthenticationFailed => "Die Anmeldung am Mailserver ist fehlgeschlagen. Bitte Benutzername und Passwort prüfen.",
        MailTestOutcome.ConnectionFailed => "Der Mailserver ist nicht erreichbar. Bitte Server und Port prüfen.",
        MailTestOutcome.TlsFailed => "Die gesicherte Verbindung (TLS) konnte nicht aufgebaut werden. Bitte Port und Verschlüsselungsmodus prüfen.",
        MailTestOutcome.Timeout => "Der Mailserver hat nicht rechtzeitig geantwortet.",
        MailTestOutcome.StorageFailed => "Die Mailserver-Einstellungen konnten nicht gespeichert werden. Bitte Datenbankverbindung prüfen.",
        _ => "Die Verbindung zum Mailserver konnte nicht geprüft werden.",
    };
}

// Abstraktion für den SMTP-Verbindungstest, damit Tests ohne Mailserver auskommen.
public interface IMailConnectionTester
{
    Task<MailTestResult> TestAsync(MailSettings settings, CancellationToken ct);
}
