using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddEvChargerDistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EvChargerDistanceMinutes",
                table: "Apartments",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvChargerDistanceMinutes",
                table: "Apartments");
        }
    }
}
