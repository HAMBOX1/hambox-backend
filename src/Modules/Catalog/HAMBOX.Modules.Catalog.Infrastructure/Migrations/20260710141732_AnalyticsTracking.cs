using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnalyticsTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductViewEvents",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductViewEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchQueryLogs",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResultCount = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchQueryLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductViewEvents_CreatedOnUtc",
                schema: "catalog",
                table: "ProductViewEvents",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProductViewEvents_ProductId",
                schema: "catalog",
                table: "ProductViewEvents",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SearchQueryLogs_CreatedOnUtc",
                schema: "catalog",
                table: "SearchQueryLogs",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SearchQueryLogs_Query",
                schema: "catalog",
                table: "SearchQueryLogs",
                column: "Query");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductViewEvents",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "SearchQueryLogs",
                schema: "catalog");
        }
    }
}
