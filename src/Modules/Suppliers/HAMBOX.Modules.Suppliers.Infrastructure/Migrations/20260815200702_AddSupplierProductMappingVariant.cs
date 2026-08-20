using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Suppliers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierProductMappingVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductId",
                schema: "suppliers",
                table: "SupplierProductMappings");

            migrationBuilder.AddColumn<Guid>(
                name: "InternalProductVariantId",
                schema: "suppliers",
                table: "SupplierProductMappings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductMappings_InternalProductVariantId",
                schema: "suppliers",
                table: "SupplierProductMappings",
                column: "InternalProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductId_ProductWide",
                schema: "suppliers",
                table: "SupplierProductMappings",
                columns: new[] { "SupplierId", "InternalProductId" },
                unique: true,
                filter: "[InternalProductVariantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductVariantId_Specific",
                schema: "suppliers",
                table: "SupplierProductMappings",
                columns: new[] { "SupplierId", "InternalProductVariantId" },
                unique: true,
                filter: "[InternalProductVariantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupplierProductMappings_InternalProductVariantId",
                schema: "suppliers",
                table: "SupplierProductMappings");

            migrationBuilder.DropIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductId_ProductWide",
                schema: "suppliers",
                table: "SupplierProductMappings");

            migrationBuilder.DropIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductVariantId_Specific",
                schema: "suppliers",
                table: "SupplierProductMappings");

            migrationBuilder.DropColumn(
                name: "InternalProductVariantId",
                schema: "suppliers",
                table: "SupplierProductMappings");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductId",
                schema: "suppliers",
                table: "SupplierProductMappings",
                columns: new[] { "SupplierId", "InternalProductId" },
                unique: true);
        }
    }
}
