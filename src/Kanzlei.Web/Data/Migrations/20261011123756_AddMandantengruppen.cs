using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanzlei.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMandantengruppen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mandantengruppen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NameNormalisiert = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mandantengruppen", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MandantengruppeMitglieder",
                columns: table => new
                {
                    MasterClientId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MandantengruppeId = table.Column<int>(type: "int", nullable: false),
                    ZugeordnetAm = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MandantengruppeMitglieder", x => x.MasterClientId);
                    table.ForeignKey(
                        name: "FK_MandantengruppeMitglieder_Mandantengruppen_MandantengruppeId",
                        column: x => x.MandantengruppeId,
                        principalTable: "Mandantengruppen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MandantengruppeMitglieder_Master_Clients_MasterClientId",
                        column: x => x.MasterClientId,
                        principalTable: "Master_Clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MandantengruppeMitglieder_MandantengruppeId",
                table: "MandantengruppeMitglieder",
                column: "MandantengruppeId");

            migrationBuilder.CreateIndex(
                name: "IX_Mandantengruppen_NameNormalisiert",
                table: "Mandantengruppen",
                column: "NameNormalisiert",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MandantengruppeMitglieder");

            migrationBuilder.DropTable(
                name: "Mandantengruppen");
        }
    }
}
