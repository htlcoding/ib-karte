using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IbKarte.Api.Daten.Migrationen
{
    /// <inheritdoc />
    public partial class Bildausschnitt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "bild_x",
                table: "eintraege",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "bild_y",
                table: "eintraege",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "bild_zoom",
                table: "eintraege",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bild_x",
                table: "eintraege");

            migrationBuilder.DropColumn(
                name: "bild_y",
                table: "eintraege");

            migrationBuilder.DropColumn(
                name: "bild_zoom",
                table: "eintraege");
        }
    }
}
