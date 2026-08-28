using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Suppliers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierRoutingAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierRoutingAuditLogs",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedSupplierProductMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedCostInBaseCurrency = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FallbackOccurred = table.Column<bool>(type: "bit", nullable: false),
                    CandidatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierRoutingAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRoutingAuditLogs_OrderId_CreatedOnUtc",
                schema: "suppliers",
                table: "SupplierRoutingAuditLogs",
                columns: new[] { "OrderId", "CreatedOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierRoutingAuditLogs",
                schema: "suppliers");
        }
    }
}
