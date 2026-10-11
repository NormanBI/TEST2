namespace Kanzlei.Web.Data;

public enum MailSslModus { Keine, StartTls, SslBeiVerbindung }

// Singleton-Datensatz (Id immer 1, per Check-Constraint erzwungen) mit den SMTP-Einstellungen.
// Das Passwort liegt ausschließlich als Data-Protection-Chiffrat vor, nie im Klartext.
public class MailEinstellungen
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; }
    public string? Benutzername { get; set; }
    public string? PasswortVerschluesselt { get; set; }
    public string AbsenderEmail { get; set; } = "";
    public string AbsenderName { get; set; } = "";
    public MailSslModus SslModus { get; set; } = MailSslModus.StartTls;
    public bool Aktiv { get; set; } = true;
    // true erst nach erfolgreichem Verbindungstest und Speichern.
    public bool Konfiguriert { get; set; }
    public DateTime? LetzterTestAm { get; set; }
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime GeaendertAm { get; set; } = DateTime.UtcNow;
}
