using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentRoomsAndContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentName",
                table: "Apartments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentPhoneNumber",
                table: "Apartments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinimumRentalPeriod",
                table: "Apartments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Apartments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerPhoneNumber",
                table: "Apartments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParkingCondition",
                table: "Apartments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParkingPoints",
                table: "Apartments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rooms",
                table: "Apartments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Older listings only stored the room count as a "Rooms: N" tag in the description.
            migrationBuilder.Sql(@"UPDATE ""Apartments""
SET ""Rooms"" = (substring(""Description"" from '(?:^|[|] )Rooms: ([0-9]+)'))::int
WHERE ""Description"" ~ '(^|[|] )Rooms: [0-9]+';");

            migrationBuilder.AddColumn<string>(
                name: "ViewType",
                table: "Apartments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentName",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "AgentPhoneNumber",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MinimumRentalPeriod",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "OwnerPhoneNumber",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "ParkingCondition",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "ParkingPoints",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Rooms",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "ViewType",
                table: "Apartments");
        }
    }
}
