using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Website_API.Data;

#nullable disable

namespace Website_API.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260728193000_NormalizeApartmentLocations")]
public partial class NormalizeApartmentLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "Apartments"
            SET "City" = 'Tbilisi',
                "Region" = 'Vake-Saburtalo',
                "District" = 'Vake',
                "Street" = 'mckheta st.'
            WHERE "Address" ILIKE '%მცხეთის%';

            UPDATE "Apartments"
            SET "City" = 'Tbilisi',
                "Region" = 'Vake-Saburtalo',
                "District" = 'Saburtalo',
                "Street" = 'shartava st.'
            WHERE "Address" ILIKE '%შარტავას%';

            UPDATE "Apartments"
            SET "City" = 'Tbilisi',
                "Region" = 'Vake-Saburtalo',
                "District" = 'Saburtalo',
                "Street" = 'm. aleksidze st.'
            WHERE "Address" ILIKE '%ალექსიძე მერაბის%';

            UPDATE "Apartments"
            SET "City" = 'Tbilisi',
                "Region" = 'Isani-Samgori',
                "District" = 'Isani',
                "Street" = 'navtlugi st.'
            WHERE "Address" ILIKE '%ნავთლუღის%';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data correction is intentionally not reversed.
    }
}
