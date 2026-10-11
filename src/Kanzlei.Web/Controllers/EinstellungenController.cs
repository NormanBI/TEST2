using Kanzlei.Web.Models;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kanzlei.Web.Controllers;

// Serverseitig nur für Administration; das Ausblenden des Menüpunkts allein genügt nicht.
[Authorize(Roles = Roles.Administration)]
public class EinstellungenController : Controller
{
    private readonly IThemeService _theme;

    private readonly IMailSettingsStore _mail;
    private readonly IMailConnectionTester _mailTester;

    public EinstellungenController(IThemeService theme, IMailSettingsStore mail, IMailConnectionTester mailTester)
    {
        _theme = theme; _mail = mail; _mailTester = mailTester;
    }

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

    // Mailserver-Einstellungen: nur Administration (Klassen-Attribut), POSTs per globalem Antiforgery-Filter geschützt.
    // Gespeichertes Passwort/Chiffrat wird nie an die View ausgegeben; ein leeres Passwortfeld behält es bei.
    [HttpGet]
    public async Task<IActionResult> Mailserver(CancellationToken ct)
    {
        var load = await _mail.LoadAsync(ct);
        if (load.Status == MailSettingsLoadStatus.Unreadable) return UnreadableMail(new MailSetupViewModel { IsEdit = true });
        var model = load.Settings is null ? new MailSetupViewModel() : MailSetupViewModel.From(load.Settings);
        model.IsEdit = true;
        return View(model);
    }

    [HttpPost, RequestSizeLimit(8192), EnableRateLimiting("mailtest")]
    public Task<IActionResult> MailserverTest(MailSetupViewModel model, CancellationToken ct) => RunMail(model, save: false, ct);

    [HttpPost, RequestSizeLimit(8192), EnableRateLimiting("mailtest")]
    public Task<IActionResult> Mailserver(MailSetupViewModel model, CancellationToken ct) => RunMail(model, save: true, ct);

    private IActionResult UnreadableMail(MailSetupViewModel model)
    {
        ViewData["Unreadable"] = true;
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("Mailserver", model);
    }

    private async Task<IActionResult> RunMail(MailSetupViewModel model, bool save, CancellationToken ct)
    {
        model.IsEdit = true;
        MailSettingsLoadResult load;
        try { load = await _mail.LoadAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            model.ClearPassword(ModelState);
            model.ResultMessage = new MailTestResult(MailTestOutcome.StorageFailed).Message;
            return View("Mailserver", model);
        }
        // Nicht entschlüsselbare Einstellungen werden nie überschrieben.
        if (load.Status == MailSettingsLoadStatus.Unreadable) return UnreadableMail(model);

        var stored = load.Settings?.Password;
        model.HasStoredPassword = stored is not null;
        var settings = model.ToSettings(ModelState, stored);
        if (settings is not null)
        {
            // Leeres Passwortfeld: gespeichertes Passwort für den Test verwenden; beim Speichern bleibt das Chiffrat unverändert.
            var testSettings = settings.HasCredentials && settings.Password is null ? settings with { Password = stored } : settings;
            var result = await _mailTester.TestAsync(testSettings, ct);
            if (result.Success && save)
            {
                try
                {
                    var saved = await _mail.SaveAsync(settings, ct);
                    result = saved == MailSaveResult.Ok ? result : new MailTestResult(MailTestOutcome.StorageFailed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result = new MailTestResult(MailTestOutcome.StorageFailed);
                }
                if (result.Success)
                {
                    TempData["Success"] = "Die Mailserver-Einstellungen wurden nach erfolgreichem Verbindungstest gespeichert.";
                    return RedirectToAction(nameof(Mailserver));
                }
            }
            model.ResultSuccess = result.Success;
            model.ResultMessage = result.Success ? "Verbindung erfolgreich. Zum Übernehmen „Testen und speichern“ wählen." : result.Message;
        }
        model.ClearPassword(ModelState);
        return View("Mailserver", model);
    }
}
