using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionInlineInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowDescriptionInline",
                schema: "catalog",
                table: "ProductOptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowDescriptionInline",
                schema: "catalog",
                table: "ProductOptionGroups",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowDescriptionInline",
                schema: "catalog",
                table: "ProductOptions");

            migrationBuilder.DropColumn(
                name: "ShowDescriptionInline",
                schema: "catalog",
                table: "ProductOptionGroups");
        }
    }
}
