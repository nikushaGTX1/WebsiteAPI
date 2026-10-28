using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddImageProcessingJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalStoragePath",
                table: "ApartmentImage",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImageProcessingJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApartmentImageId = table.Column<int>(type: "integer", nullable: true),
                    ApartmentId = table.Column<int>(type: "integer", nullable: true),
                    SourceStoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AuthorizationBasis = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PermissionReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedByUserId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Classification = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    DetectionsJson = table.Column<string>(type: "text", nullable: true),
                    WatermarkText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DuplicateOfJobId = table.Column<long>(type: "bigint", nullable: true),
                    ProcessedStoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProcessedSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GpuSeconds = table.Column<double>(type: "double precision", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    CleanOriginalRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageProcessingJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageProcessingJobs_ApartmentImage_ApartmentImageId",
                        column: x => x.ApartmentImageId,
                        principalTable: "ApartmentImage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageProcessingJobs_ApartmentImageId",
                table: "ImageProcessingJobs",
                column: "ApartmentImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageProcessingJobs_CreatedAt",
                table: "ImageProcessingJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImageProcessingJobs_SourceSha256",
                table: "ImageProcessingJobs",
                column: "SourceSha256");

            migrationBuilder.CreateIndex(
                name: "IX_ImageProcessingJobs_Status_NextAttemptAt",
                table: "ImageProcessingJobs",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageProcessingJobs");

            migrationBuilder.DropColumn(
                name: "OriginalStoragePath",
                table: "ApartmentImage");
        }
    }
}
