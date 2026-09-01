using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionnaireLinkSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "CrmQuestionnaireLinks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "CrmQuestionnaireLinks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmQuestionnaireLinks_Slug",
                table: "CrmQuestionnaireLinks",
                column: "Slug",
                unique: true,
                filter: "\"Slug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CrmQuestionnaireLinks_Token",
                table: "CrmQuestionnaireLinks",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CrmQuestionnaireLinks_Slug",
                table: "CrmQuestionnaireLinks");

            migrationBuilder.DropIndex(
                name: "IX_CrmQuestionnaireLinks_Token",
                table: "CrmQuestionnaireLinks");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "CrmQuestionnaireLinks");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "CrmQuestionnaireLinks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
