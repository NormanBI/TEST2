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

    // Erster Start: Initialen Administrator festlegen. Das Passwort wird nur als Hash in der Datenbank gespeichert.
    [HttpGet]
    public IActionResult Admin() => View(new AdminSetupViewModel());

    [HttpPost]
    public async Task<IActionResult> Admin(AdminSetupViewModel model, CancellationToken ct)
    {
        model.Vorname = model.Vorname?.Trim() ?? "";
        model.Nachname = model.Nachname?.Trim() ?? "";
        model.Emailadresse = model.Emailadresse?.Trim() ?? "";
        if (ModelState.GetValidationState(nameof(model.Passwort)) == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Valid)
        {
            var pw = PasswordPolicy.Validate(model.Passwort);
            if (pw is not null) ModelState.AddModelError(nameof(model.Passwort), pw);
            else if (model.Passwort != model.PasswortWiederholung)
                ModelState.AddModelError(nameof(model.PasswortWiederholung), "Die Passwörter stimmen nicht überein.");
        }
        if (ModelState.IsValid)
        {
            var result = await _state.CreateInitialAdministratorAsync(
                new NeuerBenutzer(model.Vorname, model.Nachname, model.Emailadresse, model.Passwort, false, true, false, false, false), ct);
            if (result is BenutzerResult.Ok or BenutzerResult.AdministratorExists) return RedirectToAction("Login", "Account");
            if (result == BenutzerResult.DuplicateEmail)
                ModelState.AddModelError(nameof(model.Emailadresse), "Diese Emailadresse wird bereits verwendet.");
        }
        model.Passwort = ""; model.PasswortWiederholung = "";
        foreach (var key in new[] { nameof(model.Passwort), nameof(model.PasswortWiederholung) })
            if (ModelState.TryGetValue(key, out var entry)) { entry.RawValue = ""; entry.AttemptedValue = ""; }
        return View(model);
    }

    // Zweiter Schritt der Ersteinrichtung (nach der Datenbank, vor dem Administrator): SMTP-Mailserver.
    // Bewusst unauthentifiziert, weil noch kein Benutzer existiert; erreichbar nur im Status MailRequired (sonst 404/Weiterleitung per Middleware).
    [HttpGet]
    public IActionResult Mail() => View(new MailSetupViewModel());

    [HttpPost]
    public Task<IActionResult> MailTest(MailSetupViewModel model, CancellationToken ct) => RunMail(model, save: false, ct);

    [HttpPost]
    public Task<IActionResult> Mail(MailSetupViewModel model, CancellationToken ct) => RunMail(model, save: true, ct);

    private async Task<IActionResult> RunMail(MailSetupViewModel model, bool save, CancellationToken ct)
    {
        var settings = model.ToSettings(ModelState, storedPassword: null);
        if (settings is not null)
        {
            var result = save
                ? await _state.ConfigureMailAsync(settings, ct)
                : await HttpContext.RequestServices.GetRequiredService<IMailConnectionTester>().TestAsync(settings, ct);
            if (result.Success && save)
                return _state.Status == DatabaseStatus.AdminRequired ? RedirectToAction(nameof(Admin)) : RedirectToAction("Login", "Account");
            model.ResultSuccess = result.Success;
            model.ResultMessage = result.Success ? "Verbindung erfolgreich. Zum Übernehmen „Testen und speichern“ wählen." : result.Message;
        }
        // Passwort wird nie zurück ins Formular gerendert.
        model.ClearPassword(ModelState);
        return View("Mail", model);
    }

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

        if (result.Success && save)
            return _state.Status == DatabaseStatus.AdminRequired ? RedirectToAction(nameof(Admin)) : RedirectToAction("Login", "Account");

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
