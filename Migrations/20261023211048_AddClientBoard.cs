using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddClientBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientIntakeLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AgentUserId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientIntakeLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientIntakeLinks_AspNetUsers_AgentUserId",
                        column: x => x.AgentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientProfiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntakeLinkId = table.Column<long>(type: "bigint", nullable: false),
                    AgentUserId = table.Column<string>(type: "text", nullable: false),
                    LeadId = table.Column<int>(type: "integer", nullable: false),
                    ProfileJson = table.Column<string>(type: "jsonb", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Nationality = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ConsentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishConsentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IdentityVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Districts = table.Column<string[]>(type: "text[]", nullable: false),
                    BudgetMinUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    BudgetMaxUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    MoveInDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MoveInTiming = table.Column<string>(type: "text", nullable: false),
                    Residents = table.Column<int>(type: "integer", nullable: false),
                    HasPets = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientProfiles_AspNetUsers_AgentUserId",
                        column: x => x.AgentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientProfiles_ClientIntakeLinks_IntakeLinkId",
                        column: x => x.IntakeLinkId,
                        principalTable: "ClientIntakeLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientProfiles_CrmLeads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "CrmLeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntakeLinks_AgentUserId",
                table: "ClientIntakeLinks",
                column: "AgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntakeLinks_CreatedAt",
                table: "ClientIntakeLinks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntakeLinks_Slug",
                table: "ClientIntakeLinks",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientIntakeLinks_Token",
                table: "ClientIntakeLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_AgentUserId",
                table: "ClientProfiles",
                column: "AgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_CreatedAt",
                table: "ClientProfiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_IntakeLinkId",
                table: "ClientProfiles",
                column: "IntakeLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_LeadId",
                table: "ClientProfiles",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_Status",
                table: "ClientProfiles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_Status_CreatedAt_Id",
                table: "ClientProfiles",
                columns: new[] { "Status", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientProfiles");

            migrationBuilder.DropTable(
                name: "ClientIntakeLinks");
        }
    }
}
