using System.ComponentModel.DataAnnotations;
using Kanzlei.Web.Data;
using Kanzlei.Web.Services;

namespace Kanzlei.Web.Models;

public class MailSetupViewModel
{
    [Required(ErrorMessage = "Bitte einen SMTP-Server angeben.")]
    [StringLength(255, ErrorMessage = "Maximal 255 Zeichen.")]
    [Display(Name = "SMTP-Server")]
    public string Host { get; set; } = "";

    [Range(1, 65535, ErrorMessage = "Port muss zwischen 1 und 65535 liegen.")]
    [Display(Name = "Port")]
    public int Port { get; set; } = 587;

    [StringLength(255, ErrorMessage = "Maximal 255 Zeichen.")]
    [Display(Name = "Benutzername (optional)")]
    public string? UserName { get; set; }

    [StringLength(256, ErrorMessage = "Maximal 256 Zeichen.")]
    [DataType(DataType.Password)]
    [Display(Name = "Passwort")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Bitte eine Absenderadresse angeben.")]
    [StringLength(254, ErrorMessage = "Maximal 254 Zeichen.")]
    [Display(Name = "Absender-Emailadresse")]
    public string SenderEmail { get; set; } = "";

    [Required(ErrorMessage = "Bitte einen Absendernamen angeben.")]
    [StringLength(100, ErrorMessage = "Maximal 100 Zeichen.")]
    [Display(Name = "Absendername")]
    public string SenderName { get; set; } = "";

    [Display(Name = "Verschlüsselung")]
    public MailSslModus Tls { get; set; } = MailSslModus.StartTls;

    // Nur Anzeige: Es wird nie das gespeicherte Passwort oder Chiffrat ausgegeben.
    public bool HasStoredPassword { get; set; }
    public bool IsEdit { get; set; }
    public string? ResultMessage { get; set; }
    public bool ResultSuccess { get; set; }

    // Das Passwort wird nie zurück ins Formular gerendert; Validierungsfehler bleiben erhalten.
    public void ClearPassword(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary state)
    {
        Password = null;
        if (state.TryGetValue(nameof(Password), out var entry)) { entry.RawValue = ""; entry.AttemptedValue = ""; }
    }

    public static MailSetupViewModel From(MailSettings s) => new()
    {
        Host = s.Host, Port = s.Port, UserName = s.UserName, SenderEmail = s.SenderEmail, SenderName = s.SenderName,
        Tls = s.Tls, HasStoredPassword = !string.IsNullOrEmpty(s.Password),
    };

    // Normalisiert die Eingabe und ergänzt Validierungsfehler. Passwort bleibt null, wenn das Feld leer ist.
    public MailSettings? ToSettings(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary state, string? storedPassword)
    {
        Host = Host?.Trim() ?? "";
        UserName = string.IsNullOrWhiteSpace(UserName) ? null : UserName.Trim();
        SenderEmail = SenderEmail?.Trim() ?? "";
        SenderName = SenderName?.Trim() ?? "";
        var password = string.IsNullOrEmpty(Password) ? null : Password;
        if (!MailSettings.IsValidHost(Host)) state.AddModelError(nameof(Host), "Ungültiger Servername (erlaubt: Buchstaben, Ziffern, . - : _).");
        if (!MailSettings.IsValidUser(UserName)) state.AddModelError(nameof(UserName), "Ungültiger Benutzername.");
        if (!MailSettings.IsValidPassword(password)) state.AddModelError(nameof(Password), "Ungültiges Passwort.");
        if (!MailSettings.IsValidEmail(SenderEmail)) state.AddModelError(nameof(SenderEmail), "Bitte eine gültige Emailadresse angeben.");
        if (!MailSettings.IsValidSenderName(SenderName)) state.AddModelError(nameof(SenderName), "Ungültiger Absendername.");
        if (!Enum.IsDefined(Tls)) state.AddModelError(nameof(Tls), "Ungültige Verschlüsselungsoption.");
        if (UserName is null) password = null;
        else
        {
            if (Tls == MailSslModus.Keine)
                state.AddModelError(nameof(Tls), "Mit Anmeldedaten ist eine Verschlüsselung (STARTTLS oder SSL/TLS) erforderlich.");
            if (password is null && storedPassword is null)
                state.AddModelError(nameof(Password), "Bitte ein Passwort angeben.");
        }
        if (!state.IsValid) return null;
        return new MailSettings(Host, Port, UserName, password, SenderEmail, SenderName, Tls);
    }
}
