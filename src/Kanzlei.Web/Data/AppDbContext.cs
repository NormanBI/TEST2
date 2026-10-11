using Microsoft.EntityFrameworkCore;

namespace Kanzlei.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Benutzer> Benutzer => Set<Benutzer>();
    public DbSet<MailEinstellungen> MailEinstellungen => Set<MailEinstellungen>();
    public DbSet<Einstellungen> Einstellungen => Set<Einstellungen>();
    public DbSet<MasterClient> MasterClients => Set<MasterClient>();
    public DbSet<BenutzerDashboardLayout> BenutzerDashboardLayouts => Set<BenutzerDashboardLayout>();

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

        modelBuilder.Entity<Einstellungen>(e =>
        {
            e.ToTable("Einstellungen", t => t.HasCheckConstraint("CK_Einstellungen_Singleton", "`Id` = 1"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Navigationstext).IsRequired().HasMaxLength(Data.Einstellungen.NavigationstextMaxLaenge);
        });

        modelBuilder.Entity<MasterClient>(e =>
        {
            e.ToTable("Master_Clients");
            e.HasKey(c => c.Id).HasName("PK_Master_Clients");
            e.Property(c => c.Id).HasColumnName("id").HasColumnType("varchar(450)").HasMaxLength(450).IsRequired().ValueGeneratedNever();
            e.Property(c => c.ClientSince).HasColumnName("client_since").HasColumnType("longtext");
            e.Property(c => c.ClientTo).HasColumnName("client_to").HasColumnType("longtext");
            e.Property(c => c.DifferingName).HasColumnName("differing_name").HasColumnType("longtext");
            e.Property(c => c.LegalPersonId).HasColumnName("legal_person_id").HasColumnType("longtext");
            e.Property(c => c.Name).HasColumnName("name").HasColumnType("longtext").IsRequired();
            e.Property(c => c.NaturalPersonId).HasColumnName("natural_person_id").HasColumnType("longtext");
            e.Property(c => c.Note).HasColumnName("note").HasColumnType("longtext");
            e.Property(c => c.Number).HasColumnName("number").HasColumnType("bigint").IsRequired();
            e.Property(c => c.Status).HasColumnName("status").HasColumnType("longtext");
            e.Property(c => c.Timestamp).HasColumnName("timestamp").HasColumnType("longtext");
            e.Property(c => c.Type).HasColumnName("type").HasColumnType("longtext").IsRequired();
            e.Property(c => c.OrganizationId).HasColumnName("organization_id").HasColumnType("longtext");
            e.Property(c => c.OrganizationName).HasColumnName("organization_name").HasColumnType("longtext");
            e.Property(c => c.OrganizationNumber).HasColumnName("organization_number").HasColumnType("bigint");
            e.Property(c => c.EstablishmentId).HasColumnName("establishment_id").HasColumnType("longtext");
            e.Property(c => c.EstablishmentName).HasColumnName("establishment_name").HasColumnType("longtext");
            e.Property(c => c.EstablishmentNumber).HasColumnName("establishment_number").HasColumnType("int");
            e.Property(c => c.EstablishmentShortName).HasColumnName("establishment_short_name").HasColumnType("longtext");
            e.Property(c => c.EstablishmentAreaId).HasColumnName("establishment_area_id").HasColumnType("longtext");
            e.Property(c => c.FunctionalAreaId).HasColumnName("functional_area_id").HasColumnType("longtext");
            e.Property(c => c.FunctionalAreaName).HasColumnName("functional_area_name").HasColumnType("longtext");
            e.Property(c => c.FunctionalAreaShortName).HasColumnName("functional_area_short_name").HasColumnType("longtext");

            // Ausschließlich fiktive Beispieldaten. HasData wird nur in der Migration einmalig eingefügt (keine Neuanlage beim Start).
            e.HasData(
                new MasterClient
                {
                    Id = "demo-mandant-0001", Name = "Beispiel Musterfirma GmbH", Number = 900001, Type = "Juristische Person",
                    Status = "Aktiv", ClientSince = "2020-01-01", Timestamp = "2026-01-15T08:00:00", Note = "Fiktiver Beispieldatensatz",
                    OrganizationId = "demo-org-001", OrganizationName = "Beispiel Musterfirma Gruppe", OrganizationNumber = 100001,
                    EstablishmentId = "demo-est-001", EstablishmentName = "Beispielstadt", EstablishmentNumber = 1,
                    EstablishmentShortName = "BSP", FunctionalAreaId = "demo-fa-001", FunctionalAreaName = "Finanzbuchhaltung",
                    FunctionalAreaShortName = "FIBU",
                },
                new MasterClient
                {
                    Id = "demo-mandant-0002", Name = "Erika Beispielfrau", Number = 900002, Type = "Natürliche Person",
                    Status = "Ruhend", ClientSince = "2022-07-01", ClientTo = "2026-06-30", Timestamp = "2026-02-20T09:30:00",
                    Note = "Fiktiver Beispieldatensatz", EstablishmentId = "demo-est-001", EstablishmentName = "Beispielstadt",
                    EstablishmentNumber = 1, EstablishmentShortName = "BSP",
                });
        });

        modelBuilder.Entity<BenutzerDashboardLayout>(e =>
        {
            e.ToTable("BenutzerDashboardLayouts");
            e.HasKey(x => x.Id);
            e.Property(x => x.LayoutJson).IsRequired().HasMaxLength(BenutzerDashboardLayout.LayoutJsonMaxLaenge);
            e.HasIndex(x => x.BenutzerId).IsUnique();
            e.HasOne(x => x.Benutzer).WithMany().HasForeignKey(x => x.BenutzerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
