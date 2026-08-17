using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedCanonicalStreets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuildingNumber",
                table: "Apartments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PropertyLatitude",
                table: "Apartments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PropertyLongitude",
                table: "Apartments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StreetId",
                table: "Apartments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LocationAreas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NameKa = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BoundaryGeoJson = table.Column<string>(type: "jsonb", nullable: true),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExternalSourceId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    GeometryStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationAreas_LocationAreas_ParentId",
                        column: x => x.ParentId,
                        principalTable: "LocationAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CanonicalStreets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CityId = table.Column<long>(type: "bigint", nullable: false),
                    DistrictId = table.Column<long>(type: "bigint", nullable: false),
                    NameKa = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Aliases = table.Column<string[]>(type: "text[]", nullable: false),
                    GeometryGeoJson = table.Column<string>(type: "jsonb", nullable: true),
                    BoundsGeoJson = table.Column<string>(type: "jsonb", nullable: true),
                    CentroidLatitude = table.Column<double>(type: "double precision", nullable: true),
                    CentroidLongitude = table.Column<double>(type: "double precision", nullable: true),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExternalSourceId = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    GeometryStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalStreets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanonicalStreets_LocationAreas_CityId",
                        column: x => x.CityId,
                        principalTable: "LocationAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CanonicalStreets_LocationAreas_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "LocationAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            var catalogCreatedAt = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
            migrationBuilder.InsertData(
                table: "LocationAreas",
                columns: new[] { "Id", "ParentId", "Type", "NameKa", "NameEn", "Slug", "BoundaryGeoJson", "Source", "ExternalSourceId", "GeometryStatus", "ApprovedAt", "UpdatedAt" },
                values: new object[,]
                {
                    { 1L, null, "city", "თბილისი", "Tbilisi", "tbilisi", null, "Velven", null, "geometry_missing", null, catalogCreatedAt },
                    { 101L, 1L, "district", "ვაკე", "Vake", "vake", null, "OpenStreetMap", "osm:relation/14900501", "geometry_missing", null, catalogCreatedAt },
                    { 102L, 1L, "district", "საბურთალო", "Saburtalo", "saburtalo", null, "OpenStreetMap", "osm:relation/5469869", "geometry_missing", null, catalogCreatedAt },
                    { 103L, 1L, "district", "ვერა", "Vera", "vera", null, "OpenStreetMap", "osm:relation/13949830", "geometry_missing", null, catalogCreatedAt },
                    { 104L, 1L, "district", "მთაწმინდა", "Mtatsminda", "mtatsminda", null, "OpenStreetMap", "osm:relation/2073140", "geometry_missing", null, catalogCreatedAt },
                    { 105L, 1L, "district", "დიდუბე", "Didube", "didube", null, "OpenStreetMap", "osm:relation/16749659", "geometry_missing", null, catalogCreatedAt },
                    { 106L, 1L, "district", "დიღომი", "Digomi", "digomi", null, "OpenStreetMap", "osm:relation/16356610", "geometry_missing", null, catalogCreatedAt },
                    { 107L, 1L, "district", "დიდი დიღომი", "Didi Digomi", "didi-digomi", null, "OpenStreetMap", "osm:relation/18183807", "geometry_missing", null, catalogCreatedAt },
                    { 108L, 1L, "district", "გლდანი", "Gldani", "gldani", null, "OpenStreetMap", "osm:relation/13438812", "geometry_missing", null, catalogCreatedAt },
                    { 109L, 1L, "district", "ნაძალადევი", "Nadzaladevi", "nadzaladevi", null, "OpenStreetMap", "osm:relation/10790351", "geometry_missing", null, catalogCreatedAt },
                    { 110L, 1L, "district", "ისანი", "Isani", "isani", null, "OpenStreetMap", "osm:relation/18467266", "geometry_missing", null, catalogCreatedAt },
                    { 111L, 1L, "district", "სამგორი", "Samgori", "samgori", null, "OpenStreetMap", "osm:relation/11300436", "geometry_missing", null, catalogCreatedAt },
                    { 112L, 1L, "district", "ავლაბარი", "Avlabari", "avlabari", null, "OpenStreetMap", "osm:relation/18467265", "geometry_missing", null, catalogCreatedAt },
                    { 113L, 1L, "district", "სოლოლაკი", "Sololaki", "sololaki", null, "OpenStreetMap", "osm:relation/2073133", "geometry_missing", null, catalogCreatedAt },
                    { 114L, 1L, "district", "ჩუღურეთი", "Chugureti", "chugureti", null, "OpenStreetMap", "osm:relation/18466649", "geometry_missing", null, catalogCreatedAt },
                    { 115L, 1L, "district", "კრწანისი", "Krtsanisi", "krtsanisi", null, "OpenStreetMap", "osm:relation/18467369", "geometry_missing", null, catalogCreatedAt },
                    { 116L, 1L, "district", "ვაშლიჯვარი", "Vashlijvari", "vashlijvari", null, "OpenStreetMap", "osm:relation/20111730", "geometry_missing", null, catalogCreatedAt }
                });
            migrationBuilder.Sql("SELECT setval(pg_get_serial_sequence('\"LocationAreas\"', 'Id'), 116, true);");

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_StreetId",
                table: "Apartments",
                column: "StreetId");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalStreets_Aliases",
                table: "CanonicalStreets",
                column: "Aliases")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalStreets_CityId",
                table: "CanonicalStreets",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalStreets_DistrictId_NameEn",
                table: "CanonicalStreets",
                columns: new[] { "DistrictId", "NameEn" });

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalStreets_DistrictId_NameKa",
                table: "CanonicalStreets",
                columns: new[] { "DistrictId", "NameKa" });

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalStreets_GeometryStatus",
                table: "CanonicalStreets",
                column: "GeometryStatus");

            migrationBuilder.CreateIndex(
                name: "IX_LocationAreas_ParentId",
                table: "LocationAreas",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationAreas_Slug",
                table: "LocationAreas",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationAreas_Type_NameEn",
                table: "LocationAreas",
                columns: new[] { "Type", "NameEn" });

            migrationBuilder.AddForeignKey(
                name: "FK_Apartments_CanonicalStreets_StreetId",
                table: "Apartments",
                column: "StreetId",
                principalTable: "CanonicalStreets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Apartments_CanonicalStreets_StreetId",
                table: "Apartments");

            migrationBuilder.DropTable(
                name: "CanonicalStreets");

            migrationBuilder.DropTable(
                name: "LocationAreas");

            migrationBuilder.DropIndex(
                name: "IX_Apartments_StreetId",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "BuildingNumber",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "PropertyLatitude",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "PropertyLongitude",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "StreetId",
                table: "Apartments");
        }
    }
}
