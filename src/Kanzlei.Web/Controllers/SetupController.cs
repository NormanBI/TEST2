using Kanzlei.Web.Models;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kanzlei.Web.Controllers;

// Erreichbar nur, solange keine Konfiguration existiert (siehe DatabaseGateMiddleware); danach 404.
[AllowAnonymous]
[RequestSizeLimit(8192)]
[EnableRateLimiting("setup")]
public class SetupController : Controller
{
    private readonly IDatabaseState _state;
    public SetupController(IDatabaseState state) => _state = state;

    [HttpGet]
    public IActionResult Index() => View(new DbSetupViewModel());

    [HttpPost]
    public Task<IActionResult> Test(DbSetupViewModel model, CancellationToken ct) => Run(model, save: false, ct);

    [HttpPost]
    public Task<IActionResult> Save(DbSetupViewModel model, CancellationToken ct) => Run(model, save: true, ct);

    [HttpGet]
    public IActionResult Status()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View(_state.Status);
    }

    private async Task<IActionResult> Run(DbSetupViewModel model, bool save, CancellationToken ct)
    {
        model.Host = model.Host?.Trim() ?? "";
        model.Database = model.Database?.Trim() ?? "";
        model.UserName = model.UserName?.Trim() ?? "";
        if (!DbConnectionSettings.IsValidHost(model.Host)) ModelState.AddModelError(nameof(model.Host), "Ungültiger Servername (erlaubt: Buchstaben, Ziffern, . - : _).");
        if (!DbConnectionSettings.IsValidDatabase(model.Database)) ModelState.AddModelError(nameof(model.Database), "Ungültiger Datenbankname (erlaubt: Buchstaben, Ziffern, _ $ -).");
        if (!DbConnectionSettings.IsValidUser(model.UserName)) ModelState.AddModelError(nameof(model.UserName), "Ungültiger Benutzername.");
        if (!DbConnectionSettings.IsValidPassword(model.Password)) ModelState.AddModelError(nameof(model.Password), "Ungültiges Passwort.");
        if (!Enum.IsDefined(model.SslMode)) ModelState.AddModelError(nameof(model.SslMode), "Ungültige SSL-Einstellung.");

        if (!ModelState.IsValid) return Respond(model);

        var settings = new DbConnectionSettings(model.Host, model.Port, model.Database, model.UserName, model.Password, model.SslMode);
        DbTestResult result;
        try
        {
            result = save ? await _state.ConfigureAsync(settings, ct) : await TestOnlyAsync(settings, ct);
        }
        catch (IOException) { result = new DbTestResult(DbTestOutcome.Other); model.ResultMessage = "Die Konfiguration konnte nicht gespeichert werden. Bitte Schreibrechte prüfen."; return Respond(model); }
        catch (UnauthorizedAccessException) { model.ResultMessage = "Die Konfiguration konnte nicht gespeichert werden. Bitte Schreibrechte prüfen."; return Respond(model); }

        if (result.Success && save) return RedirectToAction("Login", "Account");

        model.ResultSuccess = result.Success;
        model.ResultMessage = result.Success ? "Verbindung erfolgreich. Zum Übernehmen „Speichern“ wählen." : result.Message;
        return Respond(model);
    }

    private Task<DbTestResult> TestOnlyAsync(DbConnectionSettings s, CancellationToken ct) =>
        HttpContext.RequestServices.GetRequiredService<IDatabaseGateway>().TestConnectionAsync(s, ct);

    // Passwort wird nie zurück ins Formular gerendert.
    private IActionResult Respond(DbSetupViewModel model)
    {
        model.Password = "";
        ModelState.Remove(nameof(model.Password));
        return View("Index", model);
    }
}
