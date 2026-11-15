using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class ApartmentOwnerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Apartments",
                type: "text",
                nullable: true);

            // Homes already offered through "Submit apartment": link them to the account with the same email.
            migrationBuilder.Sql(@"UPDATE ""Apartments"" AS a SET ""OwnerUserId"" = u.""Id"" FROM ""OwnerSubmissions"" AS s JOIN ""AspNetUsers"" AS u ON u.""NormalizedEmail"" = UPPER(TRIM(s.""OwnerEmail"")) WHERE s.""PublishedApartmentId"" = a.""Id"" AND s.""IsGeneralSubmission"" AND s.""OwnerEmail"" IS NOT NULL AND TRIM(s.""OwnerEmail"") <> '' AND a.""OwnerUserId"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Apartments");
        }
    }
}
