using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CartItemNoVariantUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ShoppingCartId_ProductId_NoVariant",
                schema: "commerce",
                table: "CartItems",
                columns: new[] { "ShoppingCartId", "ProductId" },
                unique: true,
                filter: "[ProductVariantId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartItems_ShoppingCartId_ProductId_NoVariant",
                schema: "commerce",
                table: "CartItems");
        }
    }
}
