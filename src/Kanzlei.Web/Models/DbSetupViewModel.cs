using System.ComponentModel.DataAnnotations;
using Kanzlei.Web.Services;

namespace Kanzlei.Web.Models;

public class DbSetupViewModel
{
    [Required(ErrorMessage = "Bitte einen Server angeben.")]
    [StringLength(255, ErrorMessage = "Maximal 255 Zeichen.")]
    [Display(Name = "Server / Host")]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535, ErrorMessage = "Port muss zwischen 1 und 65535 liegen.")]
    [Display(Name = "Port")]
    public int Port { get; set; } = DbConnectionSettings.DefaultPort;

    [Required(ErrorMessage = "Bitte eine Datenbank angeben.")]
    [StringLength(64, ErrorMessage = "Maximal 64 Zeichen.")]
    [Display(Name = "Datenbank / Schema")]
    public string Database { get; set; } = "";

    [Required(ErrorMessage = "Bitte einen Benutzernamen angeben.")]
    [StringLength(80, ErrorMessage = "Maximal 80 Zeichen.")]
    [Display(Name = "Benutzername")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Bitte ein Passwort angeben.")]
    [StringLength(256, ErrorMessage = "Maximal 256 Zeichen.")]
    [DataType(DataType.Password)]
    [Display(Name = "Passwort")]
    public string Password { get; set; } = "";

    [Display(Name = "SSL/TLS")]
    public DbSslMode SslMode { get; set; } = DbSslMode.Preferred;

    public string? ResultMessage { get; set; }
    public bool ResultSuccess { get; set; }
}
