using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryFacetDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DependsOnGroupKey",
                schema: "catalog",
                table: "CategoryFacetSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DependsOnValues",
                schema: "catalog",
                table: "CategoryFacetSettings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DependsOnGroupKey",
                schema: "catalog",
                table: "CategoryFacetSettings");

            migrationBuilder.DropColumn(
                name: "DependsOnValues",
                schema: "catalog",
                table: "CategoryFacetSettings");
        }
    }
}
