using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddVelvenDiscover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MatchAlertsEmail",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MatchAlertsInApp",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "AspNetUsers",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Apartments",
                type: "timestamp with time zone",
                nullable: true);

            // Listings already public were published when staff last confirmed them (or created them).
            migrationBuilder.Sql("""
                UPDATE "Apartments"
                SET "PublishedAt" = COALESCE("LastConfirmedAt", "CreatedAt")
                WHERE "IsApproved" AND "PublishedAt" IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "SavedSearches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ProfileJson = table.Column<string>(type: "text", nullable: false),
                    CriteriaHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CriteriaSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedSearches_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SavedSearchId = table.Column<int>(type: "integer", nullable: true),
                    ApartmentId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmailPending = table.Column<bool>(type: "boolean", nullable: false),
                    EmailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientNotifications_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientNotifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientNotifications_SavedSearches_SavedSearchId",
                        column: x => x.SavedSearchId,
                        principalTable: "SavedSearches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedSearchMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SavedSearchId = table.Column<int>(type: "integer", nullable: false),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearchMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedSearchMatches_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedSearchMatches_SavedSearches_SavedSearchId",
                        column: x => x.SavedSearchId,
                        principalTable: "SavedSearches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_PublishedAt",
                table: "Apartments",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClientNotifications_ApartmentId",
                table: "ClientNotifications",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientNotifications_EmailPending",
                table: "ClientNotifications",
                column: "EmailPending",
                filter: "\"EmailPending\"");

            migrationBuilder.CreateIndex(
                name: "IX_ClientNotifications_SavedSearchId_ApartmentId",
                table: "ClientNotifications",
                columns: new[] { "SavedSearchId", "ApartmentId" },
                unique: true,
                filter: "\"SavedSearchId\" IS NOT NULL AND \"ApartmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientNotifications_UserId_ReadAt",
                table: "ClientNotifications",
                columns: new[] { "UserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_UserId_CriteriaHash",
                table: "SavedSearches",
                columns: new[] { "UserId", "CriteriaHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearchMatches_ApartmentId",
                table: "SavedSearchMatches",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearchMatches_SavedSearchId_ApartmentId",
                table: "SavedSearchMatches",
                columns: new[] { "SavedSearchId", "ApartmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientNotifications");

            migrationBuilder.DropTable(
                name: "SavedSearchMatches");

            migrationBuilder.DropTable(
                name: "SavedSearches");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_PublishedAt",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MatchAlertsEmail",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MatchAlertsInApp",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Apartments");
        }
    }
}
