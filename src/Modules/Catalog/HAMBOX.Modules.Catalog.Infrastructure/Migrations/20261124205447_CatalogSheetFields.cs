using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogSheetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SaleEndsOn",
                schema: "catalog",
                table: "ProductVariants",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                schema: "catalog",
                table: "ProductVariants",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SaleStartsOn",
                schema: "catalog",
                table: "ProductVariants",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalDescriptionAr",
                schema: "catalog",
                table: "Products",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalDescriptionEn",
                schema: "catalog",
                table: "Products",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionAr",
                schema: "catalog",
                table: "Categories",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                schema: "catalog",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaleEndsOn",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "SalePrice",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "SaleStartsOn",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "AdditionalDescriptionAr",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AdditionalDescriptionEn",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DescriptionAr",
                schema: "catalog",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                schema: "catalog",
                table: "Categories");
        }
    }
}
