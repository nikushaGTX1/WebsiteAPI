using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmRolesAndApartmentUploader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UploadedByUserId",
                table: "Apartments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_UploadedByUserId",
                table: "Apartments",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Apartments_AspNetUsers_UploadedByUserId",
                table: "Apartments",
                column: "UploadedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Apartments_AspNetUsers_UploadedByUserId",
                table: "Apartments");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_UploadedByUserId",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "Apartments");
        }
    }
}
