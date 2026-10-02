using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedInventoryCodeArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletedInventoryCodes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DigitalCode = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Pin = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PurchaseCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpirationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StatusAtDeletion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedInventoryCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeletedInventoryCodes_DeletedOnUtc",
                schema: "catalog",
                table: "DeletedInventoryCodes",
                column: "DeletedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedInventoryCodes_OriginalCodeId",
                schema: "catalog",
                table: "DeletedInventoryCodes",
                column: "OriginalCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedInventoryCodes_VariantId",
                schema: "catalog",
                table: "DeletedInventoryCodes",
                column: "VariantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedInventoryCodes",
                schema: "catalog");
        }
    }
}
