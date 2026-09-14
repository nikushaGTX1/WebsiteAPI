using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddVacancyWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "CrmJobApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConfirmed",
                table: "CrmJobApplications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CrmVacancyPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmVacancyPositions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CrmVacancyPositions",
                columns: new[] { "Id", "Title", "IsActive", "CreatedAt" },
                values: new object[,]
                {
                    { -5, "Customer support specialist", true, new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc) },
                    { -4, "Marketing specialist", true, new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc) },
                    { -3, "Property photographer", true, new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc) },
                    { -2, "Sales manager", true, new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc) },
                    { -1, "Real estate agent", true, new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmVacancyPositions_IsActive_CreatedAt",
                table: "CrmVacancyPositions",
                columns: new[] { "IsActive", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmVacancyPositions_Title",
                table: "CrmVacancyPositions",
                column: "Title",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmVacancyPositions");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "CrmJobApplications");

            migrationBuilder.DropColumn(
                name: "IsConfirmed",
                table: "CrmJobApplications");
        }
    }
}
