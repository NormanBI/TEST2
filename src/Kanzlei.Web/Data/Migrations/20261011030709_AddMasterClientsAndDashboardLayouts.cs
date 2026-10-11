using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Kanzlei.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterClientsAndDashboardLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenutzerDashboardLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BenutzerId = table.Column<int>(type: "int", nullable: false),
                    LayoutJson = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenutzerDashboardLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenutzerDashboardLayouts_Benutzer_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "Benutzer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Master_Clients",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    client_since = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    client_to = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    differing_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    legal_person_id = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    natural_person_id = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    note = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    number = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    timestamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    organization_id = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    organization_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    organization_number = table.Column<long>(type: "bigint", nullable: true),
                    establishment_id = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    establishment_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    establishment_number = table.Column<int>(type: "int", nullable: true),
                    establishment_short_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    establishment_area_id = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    functional_area_id = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    functional_area_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    functional_area_short_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Master_Clients", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Master_Clients",
                columns: new[] { "id", "client_since", "client_to", "differing_name", "establishment_area_id", "establishment_id", "establishment_name", "establishment_number", "establishment_short_name", "functional_area_id", "functional_area_name", "functional_area_short_name", "legal_person_id", "name", "natural_person_id", "note", "number", "organization_id", "organization_name", "organization_number", "status", "timestamp", "type" },
                values: new object[,]
                {
                    { "demo-mandant-0001", "2020-01-01", null, null, null, "demo-est-001", "Beispielstadt", 1, "BSP", "demo-fa-001", "Finanzbuchhaltung", "FIBU", null, "Beispiel Musterfirma GmbH", null, "Fiktiver Beispieldatensatz", 900001L, "demo-org-001", "Beispiel Musterfirma Gruppe", 100001L, "Aktiv", "2026-01-15T08:00:00", "Juristische Person" },
                    { "demo-mandant-0002", "2022-07-01", "2026-06-30", null, null, "demo-est-001", "Beispielstadt", 1, "BSP", null, null, null, null, "Erika Beispielfrau", null, "Fiktiver Beispieldatensatz", 900002L, null, null, null, "Ruhend", "2026-02-20T09:30:00", "Natürliche Person" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BenutzerDashboardLayouts_BenutzerId",
                table: "BenutzerDashboardLayouts",
                column: "BenutzerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenutzerDashboardLayouts");

            migrationBuilder.DropTable(
                name: "Master_Clients");
        }
    }
}
