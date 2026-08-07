using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresOnUtc",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReferralAuditLogs",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferralHistoryEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferrerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: true),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralHistoryEntries_Status",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralAuditLogs_OccurredOnUtc",
                schema: "commerce",
                table: "ReferralAuditLogs",
                column: "OccurredOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralAuditLogs_ReferralHistoryEntryId",
                schema: "commerce",
                table: "ReferralAuditLogs",
                column: "ReferralHistoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralAuditLogs_ReferrerUserId",
                schema: "commerce",
                table: "ReferralAuditLogs",
                column: "ReferrerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferralAuditLogs",
                schema: "commerce");

            migrationBuilder.DropIndex(
                name: "IX_ReferralHistoryEntries_Status",
                schema: "commerce",
                table: "ReferralHistoryEntries");

            migrationBuilder.DropColumn(
                name: "ExpiresOnUtc",
                schema: "commerce",
                table: "ReferralHistoryEntries");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "commerce",
                table: "ReferralHistoryEntries");
        }
    }
}
