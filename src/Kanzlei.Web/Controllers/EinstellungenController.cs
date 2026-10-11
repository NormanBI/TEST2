using Kanzlei.Web.Models;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanzlei.Web.Controllers;

// Serverseitig nur für Administration; das Ausblenden des Menüpunkts allein genügt nicht.
[Authorize(Roles = Roles.Administration)]
public class EinstellungenController : Controller
{
    private readonly IThemeService _theme;

    public EinstellungenController(IThemeService theme) => _theme = theme;

    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult Layout() => View(new LayoutViewModel(_theme.Current));

    [HttpPost]
    public IActionResult Layout(Dictionary<string, string?> values)
    {
        if (_theme.TryApply(values, out var errors))
        {
            TempData["Success"] = "Farben wurden übernommen. Sie gelten für alle Benutzer und bleiben auch nach einem Neustart der Anwendung erhalten.";
            return RedirectToAction(nameof(Layout));
        }
        var model = new LayoutViewModel(_theme.Current, values, errors);
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(model);
    }

    [HttpPost]
    public IActionResult LayoutReset()
    {
        if (_theme.Reset())
            TempData["Success"] = "Die Standardfarben wurden wiederhergestellt.";
        else
            TempData["Error"] = "Die Standardfarben konnten nicht dauerhaft gespeichert werden; das bisherige Thema bleibt aktiv.";
        return RedirectToAction(nameof(Layout));
    }
}
