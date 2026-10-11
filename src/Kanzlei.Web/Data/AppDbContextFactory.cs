using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanzlei.Web.Data;

// Nur für "dotnet ef migrations ..." zur Entwurfszeit; verbindet sich nicht und enthält keine echten Zugangsdaten.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseMySql("Server=localhost;Database=design_time", new MySqlServerVersion(new Version(8, 0, 36)))
        .Options);
}
