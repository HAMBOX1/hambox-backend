using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPendingMergeIntoProductId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PendingMergeIntoProductId",
                schema: "catalog",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_PendingMergeIntoProductId",
                schema: "catalog",
                table: "Products",
                column: "PendingMergeIntoProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Products_PendingMergeIntoProductId",
                schema: "catalog",
                table: "Products",
                column: "PendingMergeIntoProductId",
                principalSchema: "catalog",
                principalTable: "Products",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Products_PendingMergeIntoProductId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_PendingMergeIntoProductId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PendingMergeIntoProductId",
                schema: "catalog",
                table: "Products");
        }
    }
}
