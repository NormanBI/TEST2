using Microsoft.EntityFrameworkCore;

namespace Kanzlei.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Benutzer> Benutzer => Set<Benutzer>();
    public DbSet<MailEinstellungen> MailEinstellungen => Set<MailEinstellungen>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Benutzer>(e =>
        {
            e.ToTable("Benutzer");
            e.HasKey(b => b.Id);
            e.Property(b => b.Vorname).IsRequired().HasMaxLength(100);
            e.Property(b => b.Nachname).IsRequired().HasMaxLength(100);
            e.Property(b => b.Emailadresse).IsRequired().HasMaxLength(254);
            e.Property(b => b.EmailNormalisiert).IsRequired().HasMaxLength(254);
            e.Property(b => b.PasswortHash).IsRequired().HasMaxLength(512);
            e.Property(b => b.Kundennutzer).HasDefaultValue(false);
            e.Property(b => b.Administrator).HasDefaultValue(false);
            e.Property(b => b.Eigentuemer).HasDefaultValue(false);
            e.Property(b => b.Steuerberater).HasDefaultValue(false);
            e.Property(b => b.Sachbearbeiter).HasDefaultValue(false);
            // Sentinel true, damit ein ausdrücklich gesetztes false (deaktiviert) nicht vom Datenbank-Default überschrieben wird.
            e.Property(b => b.Aktiv).HasDefaultValue(true).HasSentinel(true);
            e.HasIndex(b => b.EmailNormalisiert).IsUnique();
        });

        modelBuilder.Entity<MailEinstellungen>(e =>
        {
            e.ToTable("MailEinstellungen", t => t.HasCheckConstraint("CK_MailEinstellungen_Singleton", "`Id` = 1"));
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).ValueGeneratedNever();
            e.Property(m => m.SmtpHost).IsRequired().HasMaxLength(255);
            e.Property(m => m.Benutzername).HasMaxLength(255);
            e.Property(m => m.PasswortVerschluesselt).HasMaxLength(2048);
            e.Property(m => m.AbsenderEmail).IsRequired().HasMaxLength(254);
            e.Property(m => m.AbsenderName).IsRequired().HasMaxLength(100);
            e.Property(m => m.SslModus).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Aktiv).HasDefaultValue(true).HasSentinel(true);
            e.Property(m => m.Konfiguriert).HasDefaultValue(false);
        });
    }
}
