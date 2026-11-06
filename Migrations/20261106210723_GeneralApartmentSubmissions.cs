using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class GeneralApartmentSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "LinkId",
                table: "OwnerSubmissions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "OwnerSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "OwnerSubmissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGeneralSubmission",
                table: "OwnerSubmissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "OwnerSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionKey",
                table: "OwnerSubmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubmissionClientMatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerSubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    ClientProfileId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionClientMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionClientMatches_ClientProfiles_ClientProfileId",
                        column: x => x.ClientProfileId,
                        principalTable: "ClientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmissionClientMatches_OwnerSubmissions_OwnerSubmissionId",
                        column: x => x.OwnerSubmissionId,
                        principalTable: "OwnerSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerSubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ActorUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MatchedClients = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionEvents_OwnerSubmissions_OwnerSubmissionId",
                        column: x => x.OwnerSubmissionId,
                        principalTable: "OwnerSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OwnerSubmissions_SubmissionKey",
                table: "OwnerSubmissions",
                column: "SubmissionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionClientMatches_ClientProfileId",
                table: "SubmissionClientMatches",
                column: "ClientProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionClientMatches_OwnerSubmissionId_ClientProfileId",
                table: "SubmissionClientMatches",
                columns: new[] { "OwnerSubmissionId", "ClientProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionEvents_OwnerSubmissionId_Action",
                table: "SubmissionEvents",
                columns: new[] { "OwnerSubmissionId", "Action" },
                unique: true,
                filter: "\"Action\" = 'sent'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionClientMatches");

            migrationBuilder.DropTable(
                name: "SubmissionEvents");

            migrationBuilder.DropIndex(
                name: "IX_OwnerSubmissions_SubmissionKey",
                table: "OwnerSubmissions");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "OwnerSubmissions");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "OwnerSubmissions");

            migrationBuilder.DropColumn(
                name: "IsGeneralSubmission",
                table: "OwnerSubmissions");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "OwnerSubmissions");

            migrationBuilder.DropColumn(
                name: "SubmissionKey",
                table: "OwnerSubmissions");

            migrationBuilder.AlterColumn<long>(
                name: "LinkId",
                table: "OwnerSubmissions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
