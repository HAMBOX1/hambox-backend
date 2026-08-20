using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Suppliers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueSupplierProductMappingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail loudly and do nothing else if duplicate (SupplierId, InternalProductId) rows already
            // exist — CREATE UNIQUE INDEX would fail anyway, but with a cryptic SQL Server error. This
            // aborts the whole migration transaction (nothing is altered) with an actionable message
            // instead. See docs/sql/supplier-mapping-dedup-check.sql for the reviewed, manual cleanup
            // query an operator should run before retrying this migration.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [suppliers].[SupplierProductMappings]
                    GROUP BY [SupplierId], [InternalProductId]
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    THROW 51000, 'Duplicate (SupplierId, InternalProductId) rows exist in suppliers.SupplierProductMappings. Run docs/sql/supplier-mapping-dedup-check.sql to review and resolve them before re-running this migration.', 1;
                END
                """);

            migrationBuilder.DropIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductId",
                schema: "suppliers",
                table: "SupplierProductMappings");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductId",
                schema: "suppliers",
                table: "SupplierProductMappings",
                columns: new[] { "SupplierId", "InternalProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductId",
                schema: "suppliers",
                table: "SupplierProductMappings");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductId",
                schema: "suppliers",
                table: "SupplierProductMappings",
                columns: new[] { "SupplierId", "InternalProductId" });
        }
    }
}
