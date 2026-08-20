using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDotPaymentAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentAttempts",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PartnerTxId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedCurrency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    VerifiedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VerifiedCurrency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    OperatorId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ServiceId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MaskedMsisdn = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LastReasonCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LastReasonDescription = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PendingPromotionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_OrderId",
                schema: "commerce",
                table: "PaymentAttempts",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_Provider_PartnerTxId",
                schema: "commerce",
                table: "PaymentAttempts",
                columns: new[] { "Provider", "PartnerTxId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_Provider_ProviderTransactionId",
                schema: "commerce",
                table: "PaymentAttempts",
                columns: new[] { "Provider", "ProviderTransactionId" },
                unique: true,
                filter: "[ProviderTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_Status_ExpiresOnUtc",
                schema: "commerce",
                table: "PaymentAttempts",
                columns: new[] { "Status", "ExpiresOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAttempts",
                schema: "commerce");
        }
    }
}
