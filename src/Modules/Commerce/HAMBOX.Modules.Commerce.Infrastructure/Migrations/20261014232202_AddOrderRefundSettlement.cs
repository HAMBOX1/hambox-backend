using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderRefundSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundGatewayReference",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundSettledByUserId",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RefundSettledOnUtc",
                schema: "commerce",
                table: "Orders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundSettlement",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "RefundSettlementNote",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundGatewayReference",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefundSettledByUserId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefundSettledOnUtc",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefundSettlement",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RefundSettlementNote",
                schema: "commerce",
                table: "Orders");
        }
    }
}
