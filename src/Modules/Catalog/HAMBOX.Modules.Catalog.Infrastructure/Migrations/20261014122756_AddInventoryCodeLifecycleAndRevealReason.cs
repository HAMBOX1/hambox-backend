using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryCodeLifecycleAndRevealReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "catalog",
                table: "InventoryAuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "catalog",
                table: "DigitalInventoryCodes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastStatusChangedOnUtc",
                schema: "catalog",
                table: "DigitalInventoryCodes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoldToUserId",
                schema: "catalog",
                table: "DigitalInventoryCodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "catalog",
                table: "InventoryAuditLogs");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "catalog",
                table: "DigitalInventoryCodes");

            migrationBuilder.DropColumn(
                name: "LastStatusChangedOnUtc",
                schema: "catalog",
                table: "DigitalInventoryCodes");

            migrationBuilder.DropColumn(
                name: "SoldToUserId",
                schema: "catalog",
                table: "DigitalInventoryCodes");
        }
    }
}
