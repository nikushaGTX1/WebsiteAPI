using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class AddBrowserMyHomeVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BrowserLeaseExpiresAt",
                table: "MyHomeJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrowserLeaseToken",
                table: "MyHomeJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrowserListingId",
                table: "MyHomeJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrowserUserId",
                table: "MyHomeJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "MyHomeJobs",
                type: "text",
                nullable: false,
                defaultValue: "SERVER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrowserLeaseExpiresAt",
                table: "MyHomeJobs");

            migrationBuilder.DropColumn(
                name: "BrowserLeaseToken",
                table: "MyHomeJobs");

            migrationBuilder.DropColumn(
                name: "BrowserListingId",
                table: "MyHomeJobs");

            migrationBuilder.DropColumn(
                name: "BrowserUserId",
                table: "MyHomeJobs");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "MyHomeJobs");
        }
    }
}
