using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Suppliers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierApiCallLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierApiCallLogs",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResponseSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    FulfillmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HamboxReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Attempt = table.Column<int>(type: "int", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierApiCallLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierApiCallLogs_CreatedOnUtc",
                schema: "suppliers",
                table: "SupplierApiCallLogs",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierApiCallLogs_FulfillmentId",
                schema: "suppliers",
                table: "SupplierApiCallLogs",
                column: "FulfillmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierApiCallLogs_HamboxReferenceId",
                schema: "suppliers",
                table: "SupplierApiCallLogs",
                column: "HamboxReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierApiCallLogs_OrderId",
                schema: "suppliers",
                table: "SupplierApiCallLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierApiCallLogs_SupplierId_CreatedOnUtc",
                schema: "suppliers",
                table: "SupplierApiCallLogs",
                columns: new[] { "SupplierId", "CreatedOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierApiCallLogs",
                schema: "suppliers");
        }
    }
}
