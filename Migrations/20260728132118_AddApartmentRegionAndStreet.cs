using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentRegionAndStreet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Apartments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "Apartments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_City_Region_District_Street",
                table: "Apartments",
                columns: new[] { "City", "Region", "District", "Street" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Apartments_City_Region_District_Street",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "Apartments");
        }
    }
}
