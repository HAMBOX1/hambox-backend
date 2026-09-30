using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOxaPayGateway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed the new gateway row so it shows up in the admin Payment Gateways list, disabled
            // until an admin enters real credentials and enables it — matches how the original 3
            // gateways were seeded (AddPaymentGatewayConfigurations). A second, independent crypto
            // gateway alongside "cryptomus", not a replacement.
            migrationBuilder.InsertData(
                schema: "commerce",
                table: "PaymentGatewayConfigurations",
                columns: new[] { "Id", "GatewayKey", "DisplayName", "IsEnabled", "IsTestMode", "ModifiedOnUtc", "CreatedOnUtc" },
                values: new object[] { new Guid("40000000-0000-0000-0000-000000000004"), "oxapay", "OxaPay (Crypto)", false, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "commerce",
                table: "PaymentGatewayConfigurations",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000004"));
        }
    }
}
