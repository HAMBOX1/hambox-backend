using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Suppliers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "suppliers");

            migrationBuilder.CreateTable(
                name: "SupplierAuditLogs",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierProductMappings",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalProductId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalSku = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExternalName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BuyingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProductMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AuthenticationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApiSecret = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Password = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BearerToken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OAuthSettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupportsInventorySync = table.Column<bool>(type: "bit", nullable: false),
                    SupportsPriceSync = table.Column<bool>(type: "bit", nullable: false),
                    SupportsReservations = table.Column<bool>(type: "bit", nullable: false),
                    SupportsOrderStatus = table.Column<bool>(type: "bit", nullable: false),
                    SupportsWebhooks = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAuditLogs_SupplierId_CreatedOnUtc",
                schema: "suppliers",
                table: "SupplierAuditLogs",
                columns: new[] { "SupplierId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductMappings_InternalProductId",
                schema: "suppliers",
                table: "SupplierProductMappings",
                column: "InternalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductMappings_SupplierId_InternalProductId",
                schema: "suppliers",
                table: "SupplierProductMappings",
                columns: new[] { "SupplierId", "InternalProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Code",
                schema: "suppliers",
                table: "Suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_IsDeleted",
                schema: "suppliers",
                table: "Suppliers",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Priority",
                schema: "suppliers",
                table: "Suppliers",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierAuditLogs",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "SupplierProductMappings",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "suppliers");
        }
    }
}
