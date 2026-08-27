using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddListingUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListingUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false),
                    AgentUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    AgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublishedListingId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PublishedUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingUploads_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ListingUploads_AspNetUsers_AgentUserId",
                        column: x => x.AgentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingUploads_AgentUserId",
                table: "ListingUploads",
                column: "AgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingUploads_ApartmentId_AgentUserId_Platform_PublishedLi~",
                table: "ListingUploads",
                columns: new[] { "ApartmentId", "AgentUserId", "Platform", "PublishedListingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingUploads_ApartmentId_UploadedAt",
                table: "ListingUploads",
                columns: new[] { "ApartmentId", "UploadedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingUploads");
        }
    }
}
