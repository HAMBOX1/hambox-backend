using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderHoldsPurchaseControlsAndWebhookLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HeldOnUtc",
                schema: "commerce",
                table: "Orders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoldKind",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "HoldReason",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoldReleasedByUserId",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HoldReleasedOnUtc",
                schema: "commerce",
                table: "Orders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayReference",
                schema: "commerce",
                table: "OrderPaymentCallbacks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingResult",
                schema: "commerce",
                table: "OrderPaymentCallbacks",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureVerificationResult",
                schema: "commerce",
                table: "OrderPaymentCallbacks",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentWebhookLogs",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignatureVerificationResult = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProcessingResult = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GatewayReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MerchantReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    SourceIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookLogs_OrderId",
                schema: "commerce",
                table: "PaymentWebhookLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookLogs_Provider_Result",
                schema: "commerce",
                table: "PaymentWebhookLogs",
                columns: new[] { "Provider", "ProcessingResult" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookLogs_ReceivedOnUtc",
                schema: "commerce",
                table: "PaymentWebhookLogs",
                column: "ReceivedOnUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentWebhookLogs",
                schema: "commerce");

            migrationBuilder.DropColumn(
                name: "HeldOnUtc",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HoldKind",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HoldReason",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HoldReleasedByUserId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HoldReleasedOnUtc",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GatewayReference",
                schema: "commerce",
                table: "OrderPaymentCallbacks");

            migrationBuilder.DropColumn(
                name: "ProcessingResult",
                schema: "commerce",
                table: "OrderPaymentCallbacks");

            migrationBuilder.DropColumn(
                name: "SignatureVerificationResult",
                schema: "commerce",
                table: "OrderPaymentCallbacks");
        }
    }
}
