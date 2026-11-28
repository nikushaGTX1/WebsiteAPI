using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class ApartmentWalkingRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationWalkingMinutes",
                table: "Apartments",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WalkingRoutesCalculatedAt",
                table: "Apartments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalkingRoutesError",
                table: "Apartments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalkingRoutesOriginKey",
                table: "Apartments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalkingRoutesStatus",
                table: "Apartments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotCalculated");

            migrationBuilder.CreateTable(
                name: "ApartmentWalkingRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false),
                    DestinationKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DestinationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DestinationLatitude = table.Column<double>(type: "double precision", nullable: true),
                    DestinationLongitude = table.Column<double>(type: "double precision", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    DistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    DrivingMinutes = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentWalkingRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApartmentWalkingRoutes_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_WalkingRoutesStatus",
                table: "Apartments",
                column: "WalkingRoutesStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentWalkingRoutes_ApartmentId_DestinationKey",
                table: "ApartmentWalkingRoutes",
                columns: new[] { "ApartmentId", "DestinationKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApartmentWalkingRoutes");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_WalkingRoutesStatus",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "DestinationWalkingMinutes",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "WalkingRoutesCalculatedAt",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "WalkingRoutesError",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "WalkingRoutesOriginKey",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "WalkingRoutesStatus",
                table: "Apartments");
        }
    }
}
