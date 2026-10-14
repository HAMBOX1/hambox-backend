using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAuditDetailsAndFulfillmentSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FulfillmentModeSnapshot",
                schema: "commerce",
                table: "OrderItems",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                schema: "commerce",
                table: "OrderAuditEntries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "commerce",
                table: "OrderAuditEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                schema: "commerce",
                table: "OrderAuditEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfillmentModeSnapshot",
                schema: "commerce",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                schema: "commerce",
                table: "OrderAuditEntries");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "commerce",
                table: "OrderAuditEntries");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                schema: "commerce",
                table: "OrderAuditEntries");
        }
    }
}
