namespace Kanzlei.Web.Data;

// Singleton-Datensatz (Id immer 1, per Check-Constraint erzwungen) mit den allgemeinen Anwendungseinstellungen.
public class Einstellungen
{
    public const int SingletonId = 1;
    public const string StandardNavigationstext = "Kanzlei-Prototyp";
    public const int NavigationstextMaxLaenge = 80;

    public int Id { get; set; } = SingletonId;
    public string Navigationstext { get; set; } = StandardNavigationstext;
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime GeaendertAm { get; set; } = DateTime.UtcNow;
}
