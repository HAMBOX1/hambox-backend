using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Suppliers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierFulfillments",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierProductMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedQuantity = table.Column<int>(type: "int", nullable: false),
                    DeliveredQuantity = table.Column<int>(type: "int", nullable: false),
                    HamboxReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderOrderId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    FailureCategory = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    FailureDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastReconciledOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierFulfillments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierFulfillments_HamboxReferenceId",
                schema: "suppliers",
                table: "SupplierFulfillments",
                column: "HamboxReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierFulfillments_OrderId_OrderItemId",
                schema: "suppliers",
                table: "SupplierFulfillments",
                columns: new[] { "OrderId", "OrderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierFulfillments_Status",
                schema: "suppliers",
                table: "SupplierFulfillments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierFulfillments_SupplierId_ProviderOrderId",
                schema: "suppliers",
                table: "SupplierFulfillments",
                columns: new[] { "SupplierId", "ProviderOrderId" },
                unique: true,
                filter: "ProviderOrderId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierFulfillments",
                schema: "suppliers");
        }
    }
}
