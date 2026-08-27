using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceListingUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ApartmentId",
                table: "ListingUploads",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "SourceListingId",
                table: "ListingUploads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePlatform",
                table: "ListingUploads",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "ListingUploads",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingUploads_SourcePlatform_SourceListingId_AgentUserId_P~",
                table: "ListingUploads",
                columns: new[] { "SourcePlatform", "SourceListingId", "AgentUserId", "Platform", "PublishedListingId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ListingUploads_SourcePlatform_SourceListingId_AgentUserId_P~",
                table: "ListingUploads");

            migrationBuilder.DropColumn(
                name: "SourceListingId",
                table: "ListingUploads");

            migrationBuilder.DropColumn(
                name: "SourcePlatform",
                table: "ListingUploads");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "ListingUploads");

            migrationBuilder.AlterColumn<int>(
                name: "ApartmentId",
                table: "ListingUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
