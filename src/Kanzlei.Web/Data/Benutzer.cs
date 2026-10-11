namespace Kanzlei.Web.Data;

// Benutzerkonto. Das Passwort wird ausschließlich als gesalzener, adaptiver Einweg-Hash (IPasswordHasher) gespeichert.
// Datensätze werden nie gelöscht, sondern über Aktiv deaktiviert.
public class Benutzer
{
    public int Id { get; set; }
    public string Vorname { get; set; } = "";
    public string Nachname { get; set; } = "";
    public string Emailadresse { get; set; } = "";
    // Großgeschriebene, getrimmte Form für die eindeutige, groß-/kleinschreibungsunabhängige Suche.
    public string EmailNormalisiert { get; set; } = "";
    public bool Kundennutzer { get; set; }
    public string PasswortHash { get; set; } = "";
    public bool Administrator { get; set; }
    public bool Eigentuemer { get; set; }
    public bool Steuerberater { get; set; }
    public bool Sachbearbeiter { get; set; }
    public bool Aktiv { get; set; } = true;
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime GeaendertAm { get; set; } = DateTime.UtcNow;

    public static string Normalisieren(string? email) => (email ?? "").Trim().ToUpperInvariant();
}
