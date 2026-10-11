using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanzlei.Web.Controllers;

// Serverseitig nur für Administration; das Ausblenden des Menüpunkts allein genügt nicht.
[Authorize(Roles = Roles.Administration)]
public class EinstellungenController : Controller
{
    public IActionResult Index() => View();
}
