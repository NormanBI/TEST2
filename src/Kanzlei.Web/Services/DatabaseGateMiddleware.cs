namespace Kanzlei.Web.Services;

// Erzwingt "Setup zuerst": Solange keine gültige, erreichbare Datenbankkonfiguration vorliegt, ist die Anmeldung nicht erreichbar.
public sealed class DatabaseGateMiddleware
{
    private readonly RequestDelegate _next;
    public DatabaseGateMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, IDatabaseState state)
    {
        var path = ctx.Request.Path;
        var status = await state.EnsureReadyAsync(ctx.RequestAborted);
        var isStatusPage = path.Equals("/Setup/Status", StringComparison.OrdinalIgnoreCase);
        var isSetup = path.StartsWithSegments("/Setup", StringComparison.OrdinalIgnoreCase);

        switch (status)
        {
            case DatabaseStatus.Ready:
                if (isStatusPage) { ctx.Response.Redirect("/"); return; }
                if (isSetup) { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return; }
                break;
            case DatabaseStatus.NotConfigured:
                if (!isSetup || isStatusPage) { ctx.Response.Redirect("/Setup"); return; }
                break;
            default:
                if (!isStatusPage && !path.StartsWithSegments("/theme.css", StringComparison.OrdinalIgnoreCase))
                { ctx.Response.Redirect("/Setup/Status"); return; }
                break;
        }
        await _next(ctx);
    }
}
