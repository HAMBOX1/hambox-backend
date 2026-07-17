using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryCodeRevealAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                schema: "catalog",
                table: "InventoryAuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                schema: "catalog",
                table: "InventoryAuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                schema: "catalog",
                table: "InventoryAuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAuditLogs_OrderId",
                schema: "catalog",
                table: "InventoryAuditLogs",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryAuditLogs_OrderId",
                schema: "catalog",
                table: "InventoryAuditLogs");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                schema: "catalog",
                table: "InventoryAuditLogs");

            migrationBuilder.DropColumn(
                name: "OrderId",
                schema: "catalog",
                table: "InventoryAuditLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                schema: "catalog",
                table: "InventoryAuditLogs");
        }
    }
}
