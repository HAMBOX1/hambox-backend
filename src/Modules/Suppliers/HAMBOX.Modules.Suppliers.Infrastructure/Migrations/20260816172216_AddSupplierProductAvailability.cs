using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Suppliers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierProductAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierProductAvailabilities",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierProductMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalProductId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AvailabilityState = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AvailableQuantity = table.Column<int>(type: "int", nullable: true),
                    LastCheckedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProductAvailabilities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductAvailabilities_SupplierId_ExternalProductId",
                schema: "suppliers",
                table: "SupplierProductAvailabilities",
                columns: new[] { "SupplierId", "ExternalProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductAvailabilities_SupplierProductMappingId",
                schema: "suppliers",
                table: "SupplierProductAvailabilities",
                column: "SupplierProductMappingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierProductAvailabilities",
                schema: "suppliers");
        }
    }
}
