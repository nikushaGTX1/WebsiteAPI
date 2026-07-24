using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentRatings_AgentId",
                table: "AgentRatings");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_CreatedAt",
                table: "BlogPosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_CreatedAt",
                table: "Apartments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRatings_AgentId_CreatedAt",
                table: "AgentRatings",
                columns: new[] { "AgentId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_CreatedAt",
                table: "BlogPosts");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_CreatedAt",
                table: "Apartments");

            migrationBuilder.DropIndex(
                name: "IX_AgentRatings_AgentId_CreatedAt",
                table: "AgentRatings");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRatings_AgentId",
                table: "AgentRatings",
                column: "AgentId");
        }
    }
}
