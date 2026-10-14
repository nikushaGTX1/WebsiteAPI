using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddMyHomeVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MyHomeCheckedAt",
                table: "Apartments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MyHomeId",
                table: "Apartments",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MyHomeStatus",
                table: "Apartments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MyHomeJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedBy = table.Column<string>(type: "text", nullable: true),
                    TotalApartments = table.Column<int>(type: "integer", nullable: false),
                    MissingIds = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyHomeJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MyHomeChecks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false),
                    MyHomeId = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyHomeChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyHomeChecks_MyHomeJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "MyHomeJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_MyHomeId",
                table: "Apartments",
                column: "MyHomeId");

            migrationBuilder.CreateIndex(
                name: "IX_MyHomeChecks_JobId_ApartmentId",
                table: "MyHomeChecks",
                columns: new[] { "JobId", "ApartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MyHomeChecks_JobId_Status",
                table: "MyHomeChecks",
                columns: new[] { "JobId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MyHomeJobs_State",
                table: "MyHomeJobs",
                column: "State",
                unique: true,
                filter: "\"State\" = 'RUNNING'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MyHomeChecks");

            migrationBuilder.DropTable(
                name: "MyHomeJobs");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_MyHomeId",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MyHomeCheckedAt",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MyHomeId",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "MyHomeStatus",
                table: "Apartments");
        }
    }
}
