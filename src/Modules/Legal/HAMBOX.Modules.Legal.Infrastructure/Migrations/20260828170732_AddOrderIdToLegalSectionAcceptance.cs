using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Legal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIdToLegalSectionAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                schema: "platform",
                table: "LegalSectionAcceptances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalSectionAcceptances_OrderId",
                schema: "platform",
                table: "LegalSectionAcceptances",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LegalSectionAcceptances_OrderId",
                schema: "platform",
                table: "LegalSectionAcceptances");

            migrationBuilder.DropColumn(
                name: "OrderId",
                schema: "platform",
                table: "LegalSectionAcceptances");
        }
    }
}
