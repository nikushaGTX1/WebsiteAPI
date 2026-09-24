using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentAvailabilityAndOccupancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableFrom",
                table: "Apartments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxOccupants",
                table: "Apartments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableFrom",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MaxOccupants",
                table: "Apartments");
        }
    }
}
