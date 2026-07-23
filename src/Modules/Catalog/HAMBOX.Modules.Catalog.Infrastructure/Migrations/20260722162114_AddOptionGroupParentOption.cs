using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionGroupParentOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductOptionGroups_ProductId_Key",
                schema: "catalog",
                table: "ProductOptionGroups");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentOptionId",
                schema: "catalog",
                table: "ProductOptionGroups",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionGroups_ParentOptionId_Key",
                schema: "catalog",
                table: "ProductOptionGroups",
                columns: new[] { "ParentOptionId", "Key" },
                unique: true,
                filter: "[ParentOptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionGroups_ProductId_Key",
                schema: "catalog",
                table: "ProductOptionGroups",
                columns: new[] { "ProductId", "Key" },
                unique: true,
                filter: "[ParentOptionId] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductOptionGroups_ProductOptions_ParentOptionId",
                schema: "catalog",
                table: "ProductOptionGroups",
                column: "ParentOptionId",
                principalSchema: "catalog",
                principalTable: "ProductOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductOptionGroups_ProductOptions_ParentOptionId",
                schema: "catalog",
                table: "ProductOptionGroups");

            migrationBuilder.DropIndex(
                name: "IX_ProductOptionGroups_ParentOptionId_Key",
                schema: "catalog",
                table: "ProductOptionGroups");

            migrationBuilder.DropIndex(
                name: "IX_ProductOptionGroups_ProductId_Key",
                schema: "catalog",
                table: "ProductOptionGroups");

            migrationBuilder.DropColumn(
                name: "ParentOptionId",
                schema: "catalog",
                table: "ProductOptionGroups");

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionGroups_ProductId_Key",
                schema: "catalog",
                table: "ProductOptionGroups",
                columns: new[] { "ProductId", "Key" },
                unique: true);
        }
    }
}
