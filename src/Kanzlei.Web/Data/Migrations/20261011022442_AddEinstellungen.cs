using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanzlei.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEinstellungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Einstellungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Navigationstext = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Einstellungen", x => x.Id);
                    table.CheckConstraint("CK_Einstellungen_Singleton", "`Id` = 1");
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Einstellungen");
        }
    }
}
