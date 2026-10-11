using System.ComponentModel.DataAnnotations;

namespace Kanzlei.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Bitte geben Sie Ihre E-Mail-Adresse ein.")]
    [Display(Name = "E-Mail-Adresse")]
    public string? UserName { get; set; }

    [Required(ErrorMessage = "Bitte geben Sie ein Passwort ein.")]
    [DataType(DataType.Password)]
    [Display(Name = "Passwort")]
    public string? Password { get; set; }

    public string? ReturnUrl { get; set; }
}
