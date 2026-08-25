using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLastEditedByFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastEditedByName",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEditedByUserId",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastEditedOnUtc",
                schema: "commerce",
                table: "Orders",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastEditedByName",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "LastEditedByUserId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "LastEditedOnUtc",
                schema: "commerce",
                table: "Orders");
        }
    }
}
