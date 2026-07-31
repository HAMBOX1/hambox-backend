using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EffectiveImageUrl",
                schema: "catalog",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageStorageKey",
                schema: "catalog",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                schema: "catalog",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectiveImageUrl",
                schema: "catalog",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ImageStorageKey",
                schema: "catalog",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                schema: "catalog",
                table: "Categories");
        }
    }
}
