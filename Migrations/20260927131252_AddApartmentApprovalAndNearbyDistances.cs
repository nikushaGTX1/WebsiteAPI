using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentApprovalAndNearbyDistances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CafeDistanceMinutes",
                table: "Apartments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroceryDistanceMinutes",
                table: "Apartments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Apartments",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyDistanceMinutes",
                table: "Apartments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CafeDistanceMinutes",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "GroceryDistanceMinutes",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "PharmacyDistanceMinutes",
                table: "Apartments");
        }
    }
}
