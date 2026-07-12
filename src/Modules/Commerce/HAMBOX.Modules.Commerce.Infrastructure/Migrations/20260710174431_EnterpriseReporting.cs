using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportDefinitions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormatDefault = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportDownloads",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDownloads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportFavorites",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportFavorites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledReportExecutions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DownloadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriggeredBy = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledReportExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledReports",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Format = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmailRecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NextRunOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRunOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_CreatedByUserId",
                schema: "commerce",
                table: "ReportDefinitions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_CreatedOnUtc",
                schema: "commerce",
                table: "ReportDefinitions",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_ReportType",
                schema: "commerce",
                table: "ReportDefinitions",
                column: "ReportType");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDownloads_CreatedOnUtc",
                schema: "commerce",
                table: "ReportDownloads",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDownloads_UserId",
                schema: "commerce",
                table: "ReportDownloads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportFavorites_CreatedOnUtc",
                schema: "commerce",
                table: "ReportFavorites",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReportFavorites_UserId_ReportDefinitionId",
                schema: "commerce",
                table: "ReportFavorites",
                columns: new[] { "UserId", "ReportDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReportExecutions_ScheduledReportId_StartedOnUtc",
                schema: "commerce",
                table: "ScheduledReportExecutions",
                columns: new[] { "ScheduledReportId", "StartedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReportExecutions_Status",
                schema: "commerce",
                table: "ScheduledReportExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReports_CreatedOnUtc",
                schema: "commerce",
                table: "ScheduledReports",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReports_IsEnabled_NextRunOnUtc",
                schema: "commerce",
                table: "ScheduledReports",
                columns: new[] { "IsEnabled", "NextRunOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportDefinitions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "ReportDownloads",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "ReportFavorites",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "ScheduledReportExecutions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "ScheduledReports",
                schema: "commerce");
        }
    }
}
