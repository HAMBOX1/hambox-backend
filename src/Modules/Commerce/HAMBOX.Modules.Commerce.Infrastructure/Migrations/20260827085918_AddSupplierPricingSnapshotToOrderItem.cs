using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierPricingSnapshotToOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MarginPercentAppliedAtOrderTime",
                schema: "commerce",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SelectedSupplierId",
                schema: "commerce",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SelectedSupplierProductMappingId",
                schema: "commerce",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SupplierBuyingPriceAtOrderTime",
                schema: "commerce",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarginPercentAppliedAtOrderTime",
                schema: "commerce",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SelectedSupplierId",
                schema: "commerce",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SelectedSupplierProductMappingId",
                schema: "commerce",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SupplierBuyingPriceAtOrderTime",
                schema: "commerce",
                table: "OrderItems");
        }
    }
}
