using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductInstructionsVariantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductInstructions_ProductId",
                schema: "catalog",
                table: "ProductInstructions");

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                schema: "catalog",
                table: "ProductInstructions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductInstructions_ProductId_NullVariant",
                schema: "catalog",
                table: "ProductInstructions",
                column: "ProductId",
                unique: true,
                filter: "[VariantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductInstructions_ProductId_VariantId",
                schema: "catalog",
                table: "ProductInstructions",
                columns: new[] { "ProductId", "VariantId" },
                unique: true,
                filter: "[VariantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductInstructions_ProductId_NullVariant",
                schema: "catalog",
                table: "ProductInstructions");

            migrationBuilder.DropIndex(
                name: "IX_ProductInstructions_ProductId_VariantId",
                schema: "catalog",
                table: "ProductInstructions");

            migrationBuilder.DropColumn(
                name: "VariantId",
                schema: "catalog",
                table: "ProductInstructions");

            migrationBuilder.CreateIndex(
                name: "IX_ProductInstructions_ProductId",
                schema: "catalog",
                table: "ProductInstructions",
                column: "ProductId",
                unique: true);
        }
    }
}
