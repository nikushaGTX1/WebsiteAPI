using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewSlug",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentReviews", x => x.Id);
                    table.CheckConstraint("CK_AgentReviews_Rating", "\"Rating\" >= 1 AND \"Rating\" <= 5");
                    table.ForeignKey(
                        name: "FK_AgentReviews_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ReviewSlug",
                table: "AspNetUsers",
                column: "ReviewSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentReviews_AgentId_CreatedAt",
                table: "AgentReviews",
                columns: new[] { "AgentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentReviews_AgentId_SubmissionId",
                table: "AgentReviews",
                columns: new[] { "AgentId", "SubmissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentReviews_IpHash_CreatedAt",
                table: "AgentReviews",
                columns: new[] { "IpHash", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentReviews");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ReviewSlug",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ReviewSlug",
                table: "AspNetUsers");
        }
    }
}
