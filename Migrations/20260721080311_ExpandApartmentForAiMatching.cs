using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class ExpandApartmentForAiMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApartmentStyle",
                table: "Apartments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Bathrooms",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bedrooms",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Apartments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Apartments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Floor",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GymDistanceMinutes",
                table: "Apartments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAirConditioning",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasBalcony",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasBathtub",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasDishwasher",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasElevator",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasHomeOfficeSpace",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasLargeKitchen",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasParking",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasView",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFurnished",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPetFriendly",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "KindergartenDistanceMinutes",
                table: "Apartments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Apartments",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Apartments",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetroDistanceMinutes",
                table: "Apartments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoiseLevel",
                table: "Apartments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ParkDistanceMinutes",
                table: "Apartments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolDistanceMinutes",
                table: "Apartments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SizeSquareMeters",
                table: "Apartments",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Sunlight",
                table: "Apartments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TotalFloors",
                table: "Apartments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UniversityDistanceMinutes",
                table: "Apartments",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApartmentStyle",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Bathrooms",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Bedrooms",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "GymDistanceMinutes",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "HasAirConditioning",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "HasBalcony",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "HasBathtub",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "HasDishwasher",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "HasElevator",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "HasHomeOfficeSpace",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "HasLargeKitchen",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "HasParking",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "HasView",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "IsFurnished",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "IsPetFriendly",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "KindergartenDistanceMinutes",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MetroDistanceMinutes",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "NoiseLevel",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "ParkDistanceMinutes",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "SchoolDistanceMinutes",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "SizeSquareMeters",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Sunlight",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "TotalFloors",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "UniversityDistanceMinutes",
                table: "Apartments");
        }
    }
}
