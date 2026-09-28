using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerListingLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OwnerListingLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AgentUserId = table.Column<string>(type: "text", nullable: false),
                    DealType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Uses = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerListingLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OwnerSubmissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LinkId = table.Column<long>(type: "bigint", nullable: false),
                    AgentUserId = table.Column<string>(type: "text", nullable: false),
                    DealType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OwnerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OwnerPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OwnerEmail = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    PhotoPathsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VerificationJson = table.Column<string>(type: "text", nullable: false),
                    AgentNotes = table.Column<string>(type: "text", nullable: true),
                    PublishedApartmentId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerSubmissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OwnerListingLinks_Token",
                table: "OwnerListingLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnerSubmissions_AgentUserId",
                table: "OwnerSubmissions",
                column: "AgentUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OwnerListingLinks");

            migrationBuilder.DropTable(
                name: "OwnerSubmissions");
        }
    }
}
