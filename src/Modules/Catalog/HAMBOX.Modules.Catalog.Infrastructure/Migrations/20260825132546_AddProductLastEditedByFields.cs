using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductLastEditedByFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastEditedByName",
                schema: "catalog",
                table: "Products",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEditedByUserId",
                schema: "catalog",
                table: "Products",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastEditedOnUtc",
                schema: "catalog",
                table: "Products",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastEditedByName",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastEditedByUserId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastEditedOnUtc",
                schema: "catalog",
                table: "Products");
        }
    }
}
