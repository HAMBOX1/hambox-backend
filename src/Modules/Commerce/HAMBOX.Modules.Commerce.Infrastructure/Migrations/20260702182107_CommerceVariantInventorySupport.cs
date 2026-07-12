using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommerceVariantInventorySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderLicenseKeys_OrderItemId",
                schema: "commerce",
                table: "OrderLicenseKeys");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_ShoppingCartId_ProductId",
                schema: "commerce",
                table: "CartItems");

            migrationBuilder.AlterColumn<string>(
                name: "LicenseKey",
                schema: "commerce",
                table: "OrderLicenseKeys",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<Guid>(
                name: "DigitalInventoryCodeId",
                schema: "commerce",
                table: "OrderLicenseKeys",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                schema: "commerce",
                table: "OrderLicenseKeys",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                schema: "commerce",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantSku",
                schema: "commerce",
                table: "OrderItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                schema: "commerce",
                table: "CartItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLicenseKeys_OrderItemId",
                schema: "commerce",
                table: "OrderLicenseKeys",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ShoppingCartId_ProductId_VariantId",
                schema: "commerce",
                table: "CartItems",
                columns: new[] { "ShoppingCartId", "ProductId", "ProductVariantId" },
                unique: true,
                filter: "[ProductVariantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderLicenseKeys_OrderItemId",
                schema: "commerce",
                table: "OrderLicenseKeys");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_ShoppingCartId_ProductId_VariantId",
                schema: "commerce",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "DigitalInventoryCodeId",
                schema: "commerce",
                table: "OrderLicenseKeys");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                schema: "commerce",
                table: "OrderLicenseKeys");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                schema: "commerce",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "VariantSku",
                schema: "commerce",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                schema: "commerce",
                table: "CartItems");

            migrationBuilder.AlterColumn<string>(
                name: "LicenseKey",
                schema: "commerce",
                table: "OrderLicenseKeys",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLicenseKeys_OrderItemId",
                schema: "commerce",
                table: "OrderLicenseKeys",
                column: "OrderItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ShoppingCartId_ProductId",
                schema: "commerce",
                table: "CartItems",
                columns: new[] { "ShoppingCartId", "ProductId" },
                unique: true);
        }
    }
}
