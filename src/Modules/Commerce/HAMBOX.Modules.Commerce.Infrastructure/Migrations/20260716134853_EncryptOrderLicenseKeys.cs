using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EncryptOrderLicenseKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LicenseKey",
                schema: "commerce",
                table: "OrderLicenseKeys",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "LicenseKeyHash",
                schema: "commerce",
                table: "OrderLicenseKeys",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLicenseKeys_LicenseKeyHash",
                schema: "commerce",
                table: "OrderLicenseKeys",
                column: "LicenseKeyHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderLicenseKeys_LicenseKeyHash",
                schema: "commerce",
                table: "OrderLicenseKeys");

            migrationBuilder.DropColumn(
                name: "LicenseKeyHash",
                schema: "commerce",
                table: "OrderLicenseKeys");

            migrationBuilder.AlterColumn<string>(
                name: "LicenseKey",
                schema: "commerce",
                table: "OrderLicenseKeys",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);
        }
    }
}
