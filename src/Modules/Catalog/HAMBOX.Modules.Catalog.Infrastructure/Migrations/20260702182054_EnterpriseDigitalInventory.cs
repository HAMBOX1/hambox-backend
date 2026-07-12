using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseDigitalInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigitalInventoryCodes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DigitalCode = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Pin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchaseCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SellingPriceOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpirationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReservedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SoldOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalInventoryCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryAuditLogs",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBatches",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PurchaseDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ImportDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PurchaseCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedMargin = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalCodes = table.Column<int>(type: "int", nullable: false),
                    AvailableCodes = table.Column<int>(type: "int", nullable: false),
                    ReservedCodes = table.Column<int>(type: "int", nullable: false),
                    SoldCodes = table.Column<int>(type: "int", nullable: false),
                    ReturnedCodes = table.Column<int>(type: "int", nullable: false),
                    ExpiredCodes = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryReservations",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpiresOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReleasedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventorySuppliers",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySuppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductOptionGroups",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptionGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductPlans",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PriceOverride = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ComparePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    MembershipPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LowStockThreshold = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductOptions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductOptions_ProductOptionGroups_OptionGroupId",
                        column: x => x.OptionGroupId,
                        principalSchema: "catalog",
                        principalTable: "ProductOptionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariantOptions",
                schema: "catalog",
                columns: table => new
                {
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariantOptions", x => new { x.VariantId, x.OptionId });
                    table.ForeignKey(
                        name: "FK_ProductVariantOptions_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "catalog",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalInventoryCodes_BatchId",
                schema: "catalog",
                table: "DigitalInventoryCodes",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalInventoryCodes_CodeHash",
                schema: "catalog",
                table: "DigitalInventoryCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigitalInventoryCodes_VariantId_Status",
                schema: "catalog",
                table: "DigitalInventoryCodes",
                columns: new[] { "VariantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAuditLogs_OccurredOnUtc",
                schema: "catalog",
                table: "InventoryAuditLogs",
                column: "OccurredOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAuditLogs_VariantId",
                schema: "catalog",
                table: "InventoryAuditLogs",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_SupplierId",
                schema: "catalog",
                table: "InventoryBatches",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_VariantId",
                schema: "catalog",
                table: "InventoryBatches",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_CartId",
                schema: "catalog",
                table: "InventoryReservations",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_CodeId",
                schema: "catalog",
                table: "InventoryReservations",
                column: "CodeId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_IsActive_ExpiresOnUtc",
                schema: "catalog",
                table: "InventoryReservations",
                columns: new[] { "IsActive", "ExpiresOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_UserId",
                schema: "catalog",
                table: "InventoryReservations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySuppliers_CompanyName",
                schema: "catalog",
                table: "InventorySuppliers",
                column: "CompanyName");

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionGroups_ProductId_Key",
                schema: "catalog",
                table: "ProductOptionGroups",
                columns: new[] { "ProductId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptions_OptionGroupId_Value",
                schema: "catalog",
                table: "ProductOptions",
                columns: new[] { "OptionGroupId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPlans_ProductId_Slug",
                schema: "catalog",
                table: "ProductPlans",
                columns: new[] { "ProductId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                schema: "catalog",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_Sku",
                schema: "catalog",
                table: "ProductVariants",
                column: "Sku",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigitalInventoryCodes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "InventoryAuditLogs",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "InventoryBatches",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "InventoryReservations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "InventorySuppliers",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductOptions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductPlans",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductVariantOptions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductOptionGroups",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductVariants",
                schema: "catalog");
        }
    }
}
