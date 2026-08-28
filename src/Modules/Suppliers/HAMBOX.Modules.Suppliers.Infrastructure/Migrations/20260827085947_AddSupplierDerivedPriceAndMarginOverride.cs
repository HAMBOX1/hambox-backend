using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Suppliers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierDerivedPriceAndMarginOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MarginPercentOverride",
                schema: "suppliers",
                table: "SupplierProductMappings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierDerivedPrices",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectivePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SelectedSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedSupplierProductMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedMarginPercent = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ComputedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDerivedPrices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDerivedPrices_InternalProductId",
                schema: "suppliers",
                table: "SupplierDerivedPrices",
                column: "InternalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDerivedPrices_InternalProductVariantId",
                schema: "suppliers",
                table: "SupplierDerivedPrices",
                column: "InternalProductVariantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierDerivedPrices",
                schema: "suppliers");

            migrationBuilder.DropColumn(
                name: "MarginPercentOverride",
                schema: "suppliers",
                table: "SupplierProductMappings");
        }
    }
}
