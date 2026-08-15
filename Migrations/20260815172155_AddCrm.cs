using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddCrm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmLeads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Goal = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PreferredContactMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PreferredDistricts = table.Column<string[]>(type: "text[]", nullable: false),
                    PreferredPropertyType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Bedrooms = table.Column<int>(type: "integer", nullable: true),
                    BudgetMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BudgetMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Preferences = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RequestedViewingAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ConsentGiven = table.Column<bool>(type: "boolean", nullable: false),
                    ConsentGivenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApartmentId = table.Column<int>(type: "integer", nullable: true),
                    CustomerUserId = table.Column<string>(type: "text", nullable: true),
                    AssignedAgentId = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmLeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmLeads_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmLeads_AspNetUsers_AssignedAgentId",
                        column: x => x.AssignedAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmLeads_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmLeads_AspNetUsers_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CrmActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeadId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmActivities_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmActivities_CrmLeads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "CrmLeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrmTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeadId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedAgentId = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmTasks_AspNetUsers_AssignedAgentId",
                        column: x => x.AssignedAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmTasks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmTasks_CrmLeads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "CrmLeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmActivities_CreatedByUserId",
                table: "CrmActivities",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmActivities_LeadId_CreatedAt",
                table: "CrmActivities",
                columns: new[] { "LeadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmLeads_ApartmentId",
                table: "CrmLeads",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmLeads_AssignedAgentId_Status_UpdatedAt",
                table: "CrmLeads",
                columns: new[] { "AssignedAgentId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmLeads_CreatedByUserId",
                table: "CrmLeads",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmLeads_CustomerUserId",
                table: "CrmLeads",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmLeads_Status_CreatedAt",
                table: "CrmLeads",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmTasks_AssignedAgentId_CompletedAt_DueAt",
                table: "CrmTasks",
                columns: new[] { "AssignedAgentId", "CompletedAt", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmTasks_CreatedByUserId",
                table: "CrmTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmTasks_LeadId_DueAt",
                table: "CrmTasks",
                columns: new[] { "LeadId", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmActivities");

            migrationBuilder.DropTable(
                name: "CrmTasks");

            migrationBuilder.DropTable(
                name: "CrmLeads");
        }
    }
}
