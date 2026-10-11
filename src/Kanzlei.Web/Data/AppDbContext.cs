using Microsoft.EntityFrameworkCore;

namespace Kanzlei.Web.Data;

// Persistenzgrundlage für spätere Fachdaten. Aktuell gibt es bewusst keine Entitäten;
// neue DbSet<T>-Properties werden hier ergänzt und per "dotnet ef migrations add" migriert.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
