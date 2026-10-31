using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddListingInterestNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListingInterestNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false),
                    SavedSearchId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingInterestNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingInterestNotifications_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ListingInterestNotifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ListingInterestNotifications_SavedSearches_SavedSearchId",
                        column: x => x.SavedSearchId,
                        principalTable: "SavedSearches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingInterestNotifications_ApartmentId",
                table: "ListingInterestNotifications",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingInterestNotifications_SavedSearchId",
                table: "ListingInterestNotifications",
                column: "SavedSearchId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingInterestNotifications_UserId_ApartmentId_SavedSearch~",
                table: "ListingInterestNotifications",
                columns: new[] { "UserId", "ApartmentId", "SavedSearchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingInterestNotifications_UserId_ReadAt",
                table: "ListingInterestNotifications",
                columns: new[] { "UserId", "ReadAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingInterestNotifications");
        }
    }
}
