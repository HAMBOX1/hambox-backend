using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantFulfillmentMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FulfillmentMode",
                schema: "catalog",
                table: "ProductVariants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ManualOnly");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfillmentMode",
                schema: "catalog",
                table: "ProductVariants");
        }
    }
}
