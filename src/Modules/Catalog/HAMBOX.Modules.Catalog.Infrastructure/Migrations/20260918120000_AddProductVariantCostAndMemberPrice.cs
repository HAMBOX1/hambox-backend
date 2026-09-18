using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariantCostAndMemberPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                schema: "catalog",
                table: "ProductVariants",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MemberPrice",
                schema: "catalog",
                table: "ProductVariants",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostPrice",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "MemberPrice",
                schema: "catalog",
                table: "ProductVariants");
        }
    }
}
