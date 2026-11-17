using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IbKarte.Api.Daten.Migrationen
{
    /// <inheritdoc />
    public partial class Ortssymbol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "symbol",
                table: "orte",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "symbol",
                table: "orte");
        }
    }
}
