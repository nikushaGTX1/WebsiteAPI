using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddPermanentStreetGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StreetGeometries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OsmWayId = table.Column<long>(type: "bigint", nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    District = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Names = table.Column<string[]>(type: "text[]", nullable: false),
                    CoordinatesJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreetGeometries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreetGeometries_City",
                table: "StreetGeometries",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_StreetGeometries_City_District",
                table: "StreetGeometries",
                columns: new[] { "City", "District" });

            migrationBuilder.CreateIndex(
                name: "IX_StreetGeometries_Names",
                table: "StreetGeometries",
                column: "Names")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_StreetGeometries_OsmWayId_District",
                table: "StreetGeometries",
                columns: new[] { "OsmWayId", "District" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreetGeometries");
        }
    }
}
