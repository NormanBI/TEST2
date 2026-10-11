using Kanzlei.Web.Services;
using Microsoft.AspNetCore.Authorization;
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
