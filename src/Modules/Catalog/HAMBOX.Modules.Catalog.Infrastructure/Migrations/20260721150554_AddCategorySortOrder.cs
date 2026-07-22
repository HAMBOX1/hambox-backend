using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategorySortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "catalog",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
;WITH Ordered AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY ParentId ORDER BY NameEn) - 1 AS Rn
    FROM catalog.Categories
)
UPDATE c SET c.SortOrder = o.Rn
FROM catalog.Categories c
INNER JOIN Ordered o ON o.Id = c.Id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "catalog",
                table: "Categories");
        }
    }
}
