using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAlertSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerAlertSubscriptions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    GuestSessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AlertType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastObservedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NotifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAlertSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAlertSubscriptions_AlertType_VariantId",
                schema: "commerce",
                table: "CustomerAlertSubscriptions",
                columns: new[] { "AlertType", "VariantId" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAlertSubscriptions_GuestSessionId_VariantId_AlertType",
                schema: "commerce",
                table: "CustomerAlertSubscriptions",
                columns: new[] { "GuestSessionId", "VariantId", "AlertType" },
                unique: true,
                filter: "[GuestSessionId] IS NOT NULL AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAlertSubscriptions_UserId",
                schema: "commerce",
                table: "CustomerAlertSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAlertSubscriptions_UserId_VariantId_AlertType",
                schema: "commerce",
                table: "CustomerAlertSubscriptions",
                columns: new[] { "UserId", "VariantId", "AlertType" },
                unique: true,
                filter: "[UserId] IS NOT NULL AND [IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAlertSubscriptions",
                schema: "commerce");
        }
    }
}
