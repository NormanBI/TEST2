using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanzlei.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBenutzer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Benutzer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Vorname = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nachname = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Emailadresse = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmailNormalisiert = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kundennutzer = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    PasswortHash = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Administrator = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    Eigentuemer = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    Steuerberater = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    Sachbearbeiter = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    Aktiv = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Benutzer", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Benutzer_EmailNormalisiert",
                table: "Benutzer",
                column: "EmailNormalisiert",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Benutzer");
        }
    }
}
