using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_API.Migrations
{
    /// <inheritdoc />
    public partial class CouponRedeemNoteAndVoid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RedeemNote",
                table: "ClientCoupons",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "ClientCoupons",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "ClientCoupons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedById",
                table: "ClientCoupons",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RedeemNote",
                table: "ClientCoupons");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "ClientCoupons");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "ClientCoupons");

            migrationBuilder.DropColumn(
                name: "VoidedById",
                table: "ClientCoupons");
        }
    }
}
