using System.ComponentModel.DataAnnotations;

namespace Kanzlei.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Bitte geben Sie einen Benutzernamen ein.")]
    [Display(Name = "Benutzername")]
    public string? UserName { get; set; }

    [Required(ErrorMessage = "Bitte geben Sie ein Passwort ein.")]
    [DataType(DataType.Password)]
    [Display(Name = "Passwort")]
    public string? Password { get; set; }

    public string? ReturnUrl { get; set; }
}
