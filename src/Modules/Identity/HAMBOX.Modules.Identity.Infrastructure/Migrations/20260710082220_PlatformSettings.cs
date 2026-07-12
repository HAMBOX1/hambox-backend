using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformSettingsAuditLogs",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettingsAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettingsCategories",
                schema: "identity",
                columns: table => new
                {
                    CategoryKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettingsCategories", x => x.CategoryKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettingsAuditLogs_CategoryKey",
                schema: "identity",
                table: "PlatformSettingsAuditLogs",
                column: "CategoryKey");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettingsAuditLogs_OccurredOnUtc",
                schema: "identity",
                table: "PlatformSettingsAuditLogs",
                column: "OccurredOnUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformSettingsAuditLogs",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PlatformSettingsCategories",
                schema: "identity");
        }
    }
}
