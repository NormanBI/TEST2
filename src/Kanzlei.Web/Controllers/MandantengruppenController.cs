using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanzlei.Web.Controllers;

// Auswertungen > Stammdaten > Mandantengruppen. Bewusst eigene Rollenfreigabe (ohne Sachbearbeiter), unabhängig von AuswertungenController.
[Authorize(Roles = Roles.Administration + "," + Roles.Eigentuemer + "," + Roles.Steuerberater)]
[Route("Auswertungen/Mandantengruppen")]
public class MandantengruppenController : Controller
{
    private readonly IMandantengruppenService _gruppen;
    public MandantengruppenController(IMandantengruppenService gruppen) => _gruppen = gruppen;

    public sealed record AnlegenRequest(string? Name);
    public sealed record ZuordnenRequest(string? ClientId, int GruppeId);
    public sealed record EntfernenRequest(string? ClientId);

    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("Daten")]
    public async Task<IActionResult> Daten([FromQuery] string? gruppen, [FromQuery] string? clients, CancellationToken ct)
    {
        var u = await _gruppen.ListenAsync(gruppen, clients, ct);
        return Json(new
        {
            gruppen = u.Gruppen.Select(g => new { id = g.Id, name = g.Name, anzahl = g.Anzahl }),
            clients = u.Clients.Select(c => new { id = c.Id, nummer = c.Nummer, name = c.Name, organisation = c.Organisation, niederlassung = c.Niederlassung, gruppeId = c.GruppeId }),
            gruppenGesamt = u.GruppenGesamt, clientsGesamt = u.ClientsGesamt, clientsTreffer = u.ClientsTreffer, gekuerzt = u.Gekuerzt,
        });
    }

    [HttpPost("Anlegen")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Anlegen([FromBody] AnlegenRequest? request, CancellationToken ct)
    {
        var r = await _gruppen.AnlegenAsync(request?.Name, ct);
        return r.Status switch
        {
            GruppeAnlegenStatus.Ok => Json(new { id = r.Gruppe!.Id, name = r.Gruppe.Name, anzahl = 0 }),
            GruppeAnlegenStatus.NameZuLang => BadRequest(new { fehler = "Der Gruppenname darf höchstens 100 Zeichen lang sein." }),
            GruppeAnlegenStatus.NameDoppelt => Conflict(new { fehler = "Eine Mandantengruppe mit diesem Namen existiert bereits." }),
            _ => BadRequest(new { fehler = "Bitte geben Sie einen gültigen Gruppennamen ein." }),
        };
    }

    [HttpPost("Zuordnen")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Zuordnen([FromBody] ZuordnenRequest? request, CancellationToken ct) =>
        Ergebnis(await _gruppen.ZuordnenAsync(request?.ClientId, request?.GruppeId ?? 0, ct));

    [HttpPost("Entfernen")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Entfernen([FromBody] EntfernenRequest? request, CancellationToken ct) =>
        Ergebnis(await _gruppen.EntfernenAsync(request?.ClientId, ct));

    private IActionResult Ergebnis(ZuordnungResult r) => r switch
    {
        ZuordnungResult.Ok => NoContent(),
        ZuordnungResult.GruppeNichtGefunden => NotFound(new { fehler = "Die Mandantengruppe wurde nicht gefunden." }),
        ZuordnungResult.MandantNichtGefunden => NotFound(new { fehler = "Der Mandant wurde nicht gefunden." }),
        _ => BadRequest(new { fehler = "Ungültige Eingabe." }),
    };
}
