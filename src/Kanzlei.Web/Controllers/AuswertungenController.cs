using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanzlei.Web.Controllers;

[Authorize(Roles = Roles.Administration + "," + Roles.Steuerberater + "," + Roles.Sachbearbeiter)]
public class AuswertungenController : Controller
{
    public IActionResult Index() => View();
}
