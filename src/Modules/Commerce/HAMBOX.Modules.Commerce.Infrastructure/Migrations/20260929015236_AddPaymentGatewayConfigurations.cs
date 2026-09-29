using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentGatewayConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentGatewayConfigurations",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GatewayKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsTestMode = table.Column<bool>(type: "bit", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SecondaryId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApiSecret = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WebhookUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FrontendResultUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdditionalConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentGatewayConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentGatewayConfigurations_GatewayKey",
                schema: "commerce",
                table: "PaymentGatewayConfigurations",
                column: "GatewayKey",
                unique: true);

            // Seed the 3 known gateways so the admin list is always populated, even before anyone
            // configures anything — matches every gateway's IPaymentGateway.GatewayKey exactly.
            migrationBuilder.InsertData(
                schema: "commerce",
                table: "PaymentGatewayConfigurations",
                columns: new[] { "Id", "GatewayKey", "DisplayName", "IsEnabled", "IsTestMode", "ModifiedOnUtc", "CreatedOnUtc" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), "cryptomus", "Cryptomus (Crypto/USDT)", false, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow },
                    { new Guid("40000000-0000-0000-0000-000000000002"), "dot", "DOT (Carrier Billing)", false, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow },
                    { new Guid("40000000-0000-0000-0000-000000000003"), "dotfawry", "DOT Fawry (Direct Billing)", false, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentGatewayConfigurations",
                schema: "commerce");
        }
    }
}
