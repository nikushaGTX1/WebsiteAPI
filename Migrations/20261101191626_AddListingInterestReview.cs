using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddListingInterestReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                table: "ListingInterestNotifications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnerEmailedAt",
                table: "ListingInterestNotifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnerReadAt",
                table: "ListingInterestNotifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "ListingInterestNotifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "ListingInterestNotifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerUserId",
                table: "ListingInterestNotifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ListingInterestNotifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.CreateIndex(
                name: "IX_ListingInterestNotifications_OwnerUserId_Status",
                table: "ListingInterestNotifications",
                columns: new[] { "OwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ListingInterestNotifications_ReviewerUserId_Status",
                table: "ListingInterestNotifications",
                columns: new[] { "ReviewerUserId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_ListingInterestNotifications_AspNetUsers_ReviewerUserId",
                table: "ListingInterestNotifications",
                column: "ReviewerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Matches recorded before agent review existed: the agent who listed the home reviews them.
            migrationBuilder.Sql("UPDATE \"ListingInterestNotifications\" SET \"ReviewerUserId\" = \"UserId\" WHERE \"ReviewerUserId\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ListingInterestNotifications_AspNetUsers_ReviewerUserId",
                table: "ListingInterestNotifications");

            migrationBuilder.DropIndex(
                name: "IX_ListingInterestNotifications_OwnerUserId_Status",
                table: "ListingInterestNotifications");

            migrationBuilder.DropIndex(
                name: "IX_ListingInterestNotifications_ReviewerUserId_Status",
                table: "ListingInterestNotifications");

            migrationBuilder.DropColumn(
                name: "OwnerEmail",
                table: "ListingInterestNotifications");

            migrationBuilder.DropColumn(
                name: "OwnerEmailedAt",
                table: "ListingInterestNotifications");

            migrationBuilder.DropColumn(
                name: "OwnerReadAt",
                table: "ListingInterestNotifications");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "ListingInterestNotifications");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "ListingInterestNotifications");

            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                table: "ListingInterestNotifications");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ListingInterestNotifications");
        }
    }
}
