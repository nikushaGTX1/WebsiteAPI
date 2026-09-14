using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmJobApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmJobApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Position = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Experience = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Languages = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CvStoredFileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CvOriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CvContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CvFileSize = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmJobApplications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmJobApplications_CreatedAt",
                table: "CrmJobApplications",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmJobApplications");
        }
    }
}
