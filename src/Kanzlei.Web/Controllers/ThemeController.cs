using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanzlei.Web.Controllers;

// Liefert nur validierte Hex-Farben als CSS-Variablen (auch für die Anmeldeseite, daher anonym).
[AllowAnonymous]
public class ThemeController : Controller
{
    private readonly IThemeService _theme;

    public ThemeController(IThemeService theme) => _theme = theme;

    [HttpGet("/theme.css")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Css() => Content(_theme.BuildCss(), "text/css; charset=utf-8");
}
