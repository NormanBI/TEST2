namespace Kanzlei.Web.Data;

// Pro Benutzer gespeicherte Kachelanordnung (Dashboard-Layout) der Mandanten-Auswertungen; genau eine Zeile je Benutzer.
public class BenutzerDashboardLayout
{
    public const int LayoutJsonMaxLaenge = 2000;

    public int Id { get; set; }
    public int BenutzerId { get; set; }
    public Benutzer? Benutzer { get; set; }
    public string LayoutJson { get; set; } = "[]";
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime GeaendertAm { get; set; } = DateTime.UtcNow;
}
