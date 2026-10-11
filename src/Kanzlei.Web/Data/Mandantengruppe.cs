namespace Kanzlei.Web.Data;

// Benannte Gruppe von Mandanten (Master_Clients). Der Name ist eindeutig (ohne Beachtung der Groß-/Kleinschreibung über NameNormalisiert).
public class Mandantengruppe
{
    public const int NameMaxLaenge = 100;

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameNormalisiert { get; set; } = "";
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public List<MandantengruppeMitglied> Mitglieder { get; set; } = new();

    public static string Normalisieren(string name) => name.Trim().ToUpperInvariant();
}

// Zuordnung Mandant -> Gruppe. MasterClientId ist Primärschlüssel: ein Mandant gehört höchstens einer Gruppe an.
public class MandantengruppeMitglied
{
    public string MasterClientId { get; set; } = "";
    public int MandantengruppeId { get; set; }
    public Mandantengruppe? Mandantengruppe { get; set; }
    public DateTime ZugeordnetAm { get; set; } = DateTime.UtcNow;
}
