using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Suppliers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierFulfillmentScopeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SupplierFulfillments_Scope_NonTerminal",
                schema: "suppliers",
                table: "SupplierFulfillments",
                columns: new[] { "OrderId", "OrderItemId", "SupplierId", "SupplierProductMappingId" },
                unique: true,
                filter: "Status <> 'Succeeded' AND Status <> 'PartialFailed' AND Status <> 'Failed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupplierFulfillments_Scope_NonTerminal",
                schema: "suppliers",
                table: "SupplierFulfillments");
        }
    }
}
