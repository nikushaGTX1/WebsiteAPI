using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Website_API.Data;

#nullable disable

namespace Website_API.Migrations
{
    /// <summary>Additive: two new tables, no change to existing tables or rows.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261104170000_AddApartmentOffers")]
    public partial class AddApartmentOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApartmentOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientProfileId = table.Column<long>(type: "bigint", nullable: false),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "text", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "text", nullable: true),
                    ShareWithOtherClients = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentOffers", x => x.Id);
                    table.ForeignKey("FK_ApartmentOffers_Apartments_ApartmentId", x => x.ApartmentId, "Apartments", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_ApartmentOffers_AspNetUsers_ReviewedByUserId", x => x.ReviewedByUserId, "AspNetUsers", "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey("FK_ApartmentOffers_AspNetUsers_ReviewerUserId", x => x.ReviewerUserId, "AspNetUsers", "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey("FK_ApartmentOffers_AspNetUsers_SubmittedByUserId", x => x.SubmittedByUserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_ApartmentOffers_ClientProfiles_ClientProfileId", x => x.ClientProfileId, "ClientProfiles", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApartmentOfferMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApartmentOfferId = table.Column<int>(type: "integer", nullable: false),
                    ClientProfileId = table.Column<long>(type: "bigint", nullable: false),
                    AgentUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentOfferMatches", x => x.Id);
                    table.ForeignKey("FK_ApartmentOfferMatches_ApartmentOffers_ApartmentOfferId", x => x.ApartmentOfferId, "ApartmentOffers", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_ApartmentOfferMatches_AspNetUsers_AgentUserId", x => x.AgentUserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_ApartmentOfferMatches_ClientProfiles_ClientProfileId", x => x.ClientProfileId, "ClientProfiles", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_ApartmentOffers_ApartmentId", "ApartmentOffers", "ApartmentId");
            migrationBuilder.CreateIndex("IX_ApartmentOffers_ReviewedByUserId", "ApartmentOffers", "ReviewedByUserId");
            migrationBuilder.CreateIndex("IX_ApartmentOffers_SubmittedByUserId", "ApartmentOffers", "SubmittedByUserId");
            migrationBuilder.CreateIndex("IX_ApartmentOffers_ClientProfileId_ApartmentId", "ApartmentOffers", new[] { "ClientProfileId", "ApartmentId" }, unique: true);
            migrationBuilder.CreateIndex("IX_ApartmentOffers_ReviewerUserId_Status", "ApartmentOffers", new[] { "ReviewerUserId", "Status" });
            migrationBuilder.CreateIndex("IX_ApartmentOfferMatches_ClientProfileId", "ApartmentOfferMatches", "ClientProfileId");
            migrationBuilder.CreateIndex("IX_ApartmentOfferMatches_AgentUserId_ReadAt", "ApartmentOfferMatches", new[] { "AgentUserId", "ReadAt" });
            migrationBuilder.CreateIndex("IX_ApartmentOfferMatches_ApartmentOfferId_ClientProfileId", "ApartmentOfferMatches", new[] { "ApartmentOfferId", "ClientProfileId" }, unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ApartmentOfferMatches");
            migrationBuilder.DropTable(name: "ApartmentOffers");
        }
    }
}
