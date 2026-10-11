using System.Security.Claims;
using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanzlei.Web.Controllers;

[Authorize(Roles = Roles.Administration + "," + Roles.Steuerberater + "," + Roles.Sachbearbeiter)]
public class AuswertungenController : Controller
{
    private readonly IMandantenAuswertungService _auswertung;
    private readonly IDashboardLayoutStore _layouts;

    public AuswertungenController(IMandantenAuswertungService auswertung, IDashboardLayoutStore layouts)
    {
        _auswertung = auswertung; _layouts = layouts;
    }

    public IActionResult Index() => View();

    // Auswertungen > Stammdaten > Mandanten
    [HttpGet("Auswertungen/Mandanten")]
    public async Task<IActionResult> Mandanten(CancellationToken ct)
    {
        if (!TryGetBenutzerId(out var id)) return Forbid();
        return View(await _layouts.LoadAsync(id, ct));
    }

    // Nur Kategorie und Anzahl. Fehlt ein Parameter, ist der Filter inaktiv; ein leerer Wert filtert auf "Ohne Status/Typ".
    [HttpGet("Auswertungen/Mandanten/Daten")]
    public async Task<IActionResult> MandantenDaten(CancellationToken ct)
    {
        string? status = Request.Query.TryGetValue("status", out var s) ? s.ToString().Trim() : null;
        string? typ = Request.Query.TryGetValue("typ", out var t) ? t.ToString().Trim() : null;
        var result = await _auswertung.BerechnenAsync(status, typ, ct);
        if (result is null) return BadRequest(new { fehler = "Unbekannter Filterwert." });
        return Json(result);
    }

    [HttpPost("Auswertungen/Mandanten/Layout")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> MandantenLayoutSpeichern([FromBody] List<PanelLayout>? panels, CancellationToken ct)
    {
        if (!TryGetBenutzerId(out var id)) return Forbid();
        return await _layouts.SaveAsync(id, panels, ct) switch
        {
            LayoutSaveResult.Ok => NoContent(),
            LayoutSaveResult.UserNotFound => Forbid(),
            _ => BadRequest(new { fehler = "Ungültiges Layout." }),
        };
    }

    [HttpPost("Auswertungen/Mandanten/LayoutZuruecksetzen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MandantenLayoutZuruecksetzen(CancellationToken ct)
    {
        if (!TryGetBenutzerId(out var id)) return Forbid();
        await _layouts.ResetAsync(id, ct);
        return RedirectToAction(nameof(Mandanten));
    }

    private bool TryGetBenutzerId(out int id) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out id);
}
