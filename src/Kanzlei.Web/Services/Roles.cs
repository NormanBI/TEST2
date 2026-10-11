using Kanzlei.Web.Data;

namespace Kanzlei.Web.Services;

public static class Roles
{
    public const string Administration = "Administration";
    public const string Eigentuemer = "Eigentuemer";
    public const string Steuerberater = "Steuerberater";
    public const string Sachbearbeiter = "Sachbearbeiter";

    // Rollen leiten sich ausschließlich aus den vier Rollen-Flags ab. Kundennutzer ist eine Benutzerklassifizierung, keine Rolle.
    public static IEnumerable<string> For(Benutzer b)
    {
        if (b.Administrator) yield return Administration;
        if (b.Eigentuemer) yield return Eigentuemer;
        if (b.Steuerberater) yield return Steuerberater;
        if (b.Sachbearbeiter) yield return Sachbearbeiter;
    }
}
