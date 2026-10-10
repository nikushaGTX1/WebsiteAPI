using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddPetNearbyPlaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NearbyPlaceNames",
                table: "Apartments",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PetStoreDistanceMinutes",
                table: "Apartments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VeterinaryDistanceMinutes",
                table: "Apartments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NearbyPlaceNames",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "PetStoreDistanceMinutes",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "VeterinaryDistanceMinutes",
                table: "Apartments");
        }
    }
}
