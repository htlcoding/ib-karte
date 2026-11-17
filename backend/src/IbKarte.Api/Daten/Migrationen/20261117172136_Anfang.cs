using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IbKarte.Api.Daten.Migrationen
{
    /// <inheritdoc />
    public partial class Anfang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "datenstand",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    stand = table.Column<string>(type: "text", nullable: false),
                    importiert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pruefsumme = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_datenstand", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "offene_fragen",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offene_fragen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orte",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    adresse = table.Column<string>(type: "text", nullable: true),
                    lat = table.Column<double>(type: "double precision", nullable: true),
                    lon = table.Column<double>(type: "double precision", nullable: true),
                    genauigkeit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quelle = table.Column<string>(type: "text", nullable: true),
                    quelle_url = table.Column<string>(type: "text", nullable: true),
                    land = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orte", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "quellen",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titel = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quellen", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "eintraege",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    typ = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gruppe = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    umfeld = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    form = table.Column<string>(type: "text", nullable: false),
                    info = table.Column<string>(type: "text", nullable: false),
                    ordner = table.Column<string>(type: "text", nullable: true),
                    ort_key = table.Column<string>(type: "character varying(80)", nullable: true),
                    ort_grund = table.Column<string>(type: "text", nullable: true),
                    bild_url = table.Column<string>(type: "text", nullable: true),
                    bild_quelle = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eintraege", x => x.id);
                    table.CheckConstraint("ck_eintraege_typ", "typ IN ('person','org','medium','ereignis')");
                    table.ForeignKey(
                        name: "FK_eintraege_orte_ort_key",
                        column: x => x.ort_key,
                        principalTable: "orte",
                        principalColumn: "key",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "eintrag_quellen",
                columns: table => new
                {
                    eintrag_id = table.Column<string>(type: "character varying(80)", nullable: false),
                    quelle_id = table.Column<string>(type: "character varying(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eintrag_quellen", x => new { x.eintrag_id, x.quelle_id });
                    table.ForeignKey(
                        name: "FK_eintrag_quellen_eintraege_eintrag_id",
                        column: x => x.eintrag_id,
                        principalTable: "eintraege",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_eintrag_quellen_quellen_quelle_id",
                        column: x => x.quelle_id,
                        principalTable: "quellen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "verbindungen",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    von_id = table.Column<string>(type: "character varying(80)", nullable: false),
                    nach_id = table.Column<string>(type: "character varying(80)", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    art = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verbindungen", x => x.id);
                    table.CheckConstraint("ck_verbindungen_status", "status IN ('belegt','vorwurf')");
                    table.ForeignKey(
                        name: "FK_verbindungen_eintraege_nach_id",
                        column: x => x.nach_id,
                        principalTable: "eintraege",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_verbindungen_eintraege_von_id",
                        column: x => x.von_id,
                        principalTable: "eintraege",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "verbindung_quellen",
                columns: table => new
                {
                    verbindung_id = table.Column<int>(type: "integer", nullable: false),
                    quelle_id = table.Column<string>(type: "character varying(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verbindung_quellen", x => new { x.verbindung_id, x.quelle_id });
                    table.ForeignKey(
                        name: "FK_verbindung_quellen_quellen_quelle_id",
                        column: x => x.quelle_id,
                        principalTable: "quellen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_verbindung_quellen_verbindungen_verbindung_id",
                        column: x => x.verbindung_id,
                        principalTable: "verbindungen",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eintraege_name",
                table: "eintraege",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_eintraege_ort_key",
                table: "eintraege",
                column: "ort_key");

            migrationBuilder.CreateIndex(
                name: "IX_eintrag_quellen_quelle_id",
                table: "eintrag_quellen",
                column: "quelle_id");

            migrationBuilder.CreateIndex(
                name: "IX_verbindung_quellen_quelle_id",
                table: "verbindung_quellen",
                column: "quelle_id");

            migrationBuilder.CreateIndex(
                name: "IX_verbindungen_nach_id",
                table: "verbindungen",
                column: "nach_id");

            migrationBuilder.CreateIndex(
                name: "IX_verbindungen_von_id",
                table: "verbindungen",
                column: "von_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "datenstand");

            migrationBuilder.DropTable(
                name: "eintrag_quellen");

            migrationBuilder.DropTable(
                name: "offene_fragen");

            migrationBuilder.DropTable(
                name: "verbindung_quellen");

            migrationBuilder.DropTable(
                name: "quellen");

            migrationBuilder.DropTable(
                name: "verbindungen");

            migrationBuilder.DropTable(
                name: "eintraege");

            migrationBuilder.DropTable(
                name: "orte");
        }
    }
}
