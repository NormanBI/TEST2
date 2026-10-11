using Kanzlei.Web.Models;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanzlei.Web.Controllers;

// Serverseitig nur für Administration. Bewusst keine Lösch-Aktion: Benutzer werden nur deaktiviert.
[Authorize(Roles = Roles.Administration)]
public class BenutzerController : Controller
{
    private readonly IBenutzerStore _users;
    public BenutzerController(IBenutzerStore users) => _users = users;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _users.ListAsync(ct));

    [HttpGet]
    public IActionResult Create() => View(new BenutzerCreateViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(BenutzerCreateViewModel model, CancellationToken ct)
    {
        model.Vorname = model.Vorname?.Trim() ?? "";
        model.Nachname = model.Nachname?.Trim() ?? "";
        model.Emailadresse = model.Emailadresse?.Trim() ?? "";
        var pwError = ModelState.GetValidationState(nameof(model.Passwort)) == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Valid
            ? PasswordPolicy.Validate(model.Passwort) : null;
        if (pwError is not null) ModelState.AddModelError(nameof(model.Passwort), pwError);

        if (ModelState.IsValid)
        {
            var result = await _users.CreateAsync(model.ToNeu(), ct);
            if (result == BenutzerResult.Ok)
            {
                TempData["Success"] = "Der Benutzer wurde angelegt.";
                return RedirectToAction(nameof(Index));
            }
            if (result == BenutzerResult.DuplicateEmail)
                ModelState.AddModelError(nameof(model.Emailadresse), "Diese Emailadresse wird bereits verwendet.");
        }

        // Passwort wird nie zurück ins Formular gerendert.
        model.Passwort = "";
        ClearAttempted(nameof(model.Passwort));
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(model);
    }

    // Fehlermeldungen bleiben erhalten, nur der eingegebene Wert wird nicht erneut gerendert.
    private void ClearAttempted(string key)
    {
        if (ModelState.TryGetValue(key, out var entry)) { entry.RawValue = ""; entry.AttemptedValue = ""; }
    }

    [HttpPost]
    public Task<IActionResult> Deaktivieren(int id, CancellationToken ct) => SetActive(id, false, ct);

    [HttpPost]
    public Task<IActionResult> Aktivieren(int id, CancellationToken ct) => SetActive(id, true, ct);

    private async Task<IActionResult> SetActive(int id, bool active, CancellationToken ct)
    {
        var result = await _users.SetActiveAsync(id, active, ct);
        switch (result)
        {
            case BenutzerResult.Ok:
                TempData["Success"] = active ? "Der Benutzer wurde aktiviert." : "Der Benutzer wurde deaktiviert; bestehende Sitzungen werden beendet.";
                break;
            case BenutzerResult.LastAdministrator:
                TempData["Error"] = "Der letzte aktive Administrator kann nicht deaktiviert werden.";
                break;
            default:
                return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }
}
