using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using Syncfusion.Licensing;

var builder = WebApplication.CreateBuilder(args);

// Syncfusion-Lizenzschlüssel nur aus Konfiguration/Umgebung (z. B. SYNCFUSION_LICENSE_KEY), nie im Repository.
var licenseKey = builder.Configuration["SYNCFUSION_LICENSE_KEY"]
    ?? builder.Configuration["Syncfusion:LicenseKey"];
if (!string.IsNullOrWhiteSpace(licenseKey))
{
    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
}

builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "Kanzlei.Demo";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = false;
    });

builder.Services.AddAuthorization(options =>
{
    // Standard: jede Seite erfordert Anmeldung, außer sie ist ausdrücklich [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));

// Speicherort der Theme-CSS-Datei: THEME_CSS_FILE bzw. Theme:CssFile, Standard App_Data/theme.css unter dem Content-Root (außerhalb wwwroot).
builder.Services.AddSingleton<IThemeService>(sp =>
{
    var configured = builder.Configuration["THEME_CSS_FILE"] ?? builder.Configuration["Theme:CssFile"];
    var path = string.IsNullOrWhiteSpace(configured)
        ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "theme.css")
        : Path.GetFullPath(configured, builder.Environment.ContentRootPath);
    return new ThemeService(path, sp.GetRequiredService<ILogger<ThemeService>>());
});

// Datenbank-Konfiguration (DB_CONFIG_FILE / Database:ConfigFile) und Data-Protection-Schlüsselbund (DATAPROTECTION_KEYS_PATH / DataProtection:KeysPath);
// Standard unter App_Data im Content-Root, nie unter wwwroot.
string ResolvePath(string? configured, params string[] defaultParts) => string.IsNullOrWhiteSpace(configured)
    ? Path.Combine(new[] { builder.Environment.ContentRootPath }.Concat(defaultParts).ToArray())
    : Path.GetFullPath(configured, builder.Environment.ContentRootPath);
var dbConfigPath = ResolvePath(builder.Configuration["Database:ConfigFile"] ?? builder.Configuration["DB_CONFIG_FILE"], "App_Data", "db-settings.json");
var keysPath = ResolvePath(builder.Configuration["DataProtection:KeysPath"] ?? builder.Configuration["DATAPROTECTION_KEYS_PATH"], "App_Data", "keys");

builder.Services.AddDataProtection()
    .SetApplicationName("Kanzlei.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddSingleton<IDbSettingsStore>(sp =>
    new DbSettingsStore(dbConfigPath, sp.GetRequiredService<IDataProtectionProvider>(), sp.GetRequiredService<ILogger<DbSettingsStore>>()));
builder.Services.AddSingleton<IDatabaseGateway, MySqlDatabaseGateway>();
builder.Services.AddSingleton<IDatabaseState, DatabaseState>();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("setup", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseMiddleware<DatabaseGateMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Beim Start Konfiguration laden und Migrationen anwenden; Fehler blockieren den Start nicht (Status-/Setup-Seite).
try { await app.Services.GetRequiredService<IDatabaseState>().EnsureReadyAsync(CancellationToken.None); }
catch (Exception ex) { app.Logger.LogError("Datenbankinitialisierung beim Start fehlgeschlagen ({ExceptionType}).", ex.GetType().Name); }

app.Run();

public partial class Program { }
