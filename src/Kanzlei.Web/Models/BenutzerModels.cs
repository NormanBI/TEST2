using System.ComponentModel.DataAnnotations;
using Kanzlei.Web.Services;

namespace Kanzlei.Web.Models;

public class BenutzerCreateViewModel
{
    [Required(ErrorMessage = "Bitte einen Vornamen angeben.")]
    [StringLength(100, ErrorMessage = "Maximal 100 Zeichen.")]
    [Display(Name = "Vorname")]
    public string Vorname { get; set; } = "";

    [Required(ErrorMessage = "Bitte einen Nachnamen angeben.")]
    [StringLength(100, ErrorMessage = "Maximal 100 Zeichen.")]
    [Display(Name = "Nachname")]
    public string Nachname { get; set; } = "";

    [Required(ErrorMessage = "Bitte eine E-Mail-Adresse angeben.")]
    [StringLength(254, ErrorMessage = "Maximal 254 Zeichen.")]
    [EmailAddress(ErrorMessage = "Bitte eine gültige E-Mail-Adresse angeben.")]
    [Display(Name = "Emailadresse")]
    public string Emailadresse { get; set; } = "";

    [Required(ErrorMessage = "Bitte ein Passwort angeben.")]
    [DataType(DataType.Password)]
    [Display(Name = "Initiales Passwort")]
    public string Passwort { get; set; } = "";

    [Display(Name = "Kundennutzer")]
    public bool Kundennutzer { get; set; }

    [Display(Name = "Administrator")]
    public bool Administrator { get; set; }

    [Display(Name = "Eigentümer")]
    public bool Eigentuemer { get; set; }

    [Display(Name = "Steuerberater")]
    public bool Steuerberater { get; set; }

    [Display(Name = "Sachbearbeiter")]
    public bool Sachbearbeiter { get; set; }

    public NeuerBenutzer ToNeu() => new(Vorname, Nachname, Emailadresse, Passwort, Kundennutzer,
        Administrator, Eigentuemer, Steuerberater, Sachbearbeiter);
}

public class AdminSetupViewModel
{
    [Required(ErrorMessage = "Bitte einen Vornamen angeben.")]
    [StringLength(100, ErrorMessage = "Maximal 100 Zeichen.")]
    [Display(Name = "Vorname")]
    public string Vorname { get; set; } = "";

    [Required(ErrorMessage = "Bitte einen Nachnamen angeben.")]
    [StringLength(100, ErrorMessage = "Maximal 100 Zeichen.")]
    [Display(Name = "Nachname")]
    public string Nachname { get; set; } = "";

    [Required(ErrorMessage = "Bitte eine E-Mail-Adresse angeben.")]
    [StringLength(254, ErrorMessage = "Maximal 254 Zeichen.")]
    [EmailAddress(ErrorMessage = "Bitte eine gültige E-Mail-Adresse angeben.")]
    [Display(Name = "Emailadresse")]
    public string Emailadresse { get; set; } = "";

    [Required(ErrorMessage = "Bitte ein Passwort angeben.")]
    [DataType(DataType.Password)]
    [Display(Name = "Passwort")]
    public string Passwort { get; set; } = "";

    [Required(ErrorMessage = "Bitte das Passwort wiederholen.")]
    [DataType(DataType.Password)]
    [Display(Name = "Passwort wiederholen")]
    public string PasswortWiederholung { get; set; } = "";
}
