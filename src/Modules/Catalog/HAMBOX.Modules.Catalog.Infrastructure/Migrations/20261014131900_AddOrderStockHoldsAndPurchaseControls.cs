using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderStockHoldsAndPurchaseControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxQuantityPerOrder",
                schema: "catalog",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxQuantityPerUserPerDay",
                schema: "catalog",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresManualReview",
                schema: "catalog",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                schema: "catalog",
                table: "InventoryReservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReleaseReason",
                schema: "catalog",
                table: "InventoryReservations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_OrderId",
                schema: "catalog",
                table: "InventoryReservations",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryReservations_OrderId",
                schema: "catalog",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "MaxQuantityPerOrder",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MaxQuantityPerUserPerDay",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RequiresManualReview",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OrderId",
                schema: "catalog",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "ReleaseReason",
                schema: "catalog",
                table: "InventoryReservations");
        }
    }
}
