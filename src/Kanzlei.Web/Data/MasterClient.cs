namespace Kanzlei.Web.Data;

// Mandantenstammdaten (Tabelle Master_Clients). Spalten- und Tabellennamen entsprechen exakt dem SQL-Server-Quellschema;
// nvarchar(max) wird zu LONGTEXT, nvarchar(450) zu varchar(450), bigint zu bigint, int zu int.
public class MasterClient
{
    public string Id { get; set; } = "";
    public string? ClientSince { get; set; }
    public string? ClientTo { get; set; }
    public string? DifferingName { get; set; }
    public string? LegalPersonId { get; set; }
    public string Name { get; set; } = "";
    public string? NaturalPersonId { get; set; }
    public string? Note { get; set; }
    public long Number { get; set; }
    public string? Status { get; set; }
    public string? Timestamp { get; set; }
    public string Type { get; set; } = "";
    public string? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public long? OrganizationNumber { get; set; }
    public string? EstablishmentId { get; set; }
    public string? EstablishmentName { get; set; }
    public int? EstablishmentNumber { get; set; }
    public string? EstablishmentShortName { get; set; }
    public string? EstablishmentAreaId { get; set; }
    public string? FunctionalAreaId { get; set; }
    public string? FunctionalAreaName { get; set; }
    public string? FunctionalAreaShortName { get; set; }
}
