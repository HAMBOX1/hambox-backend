using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiRequestLogs",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RequestSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ResponseSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Exception = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiRequestLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationalAlerts",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcknowledgedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationalAuditLogs",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationalJobs",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    WorkerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RelatedEntityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExceptionDetails = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRetryOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextVisibleOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_Path",
                schema: "commerce",
                table: "ApiRequestLogs",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_StatusCode",
                schema: "commerce",
                table: "ApiRequestLogs",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_TimestampUtc",
                schema: "commerce",
                table: "ApiRequestLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAlerts_Code",
                schema: "commerce",
                table: "OperationalAlerts",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAlerts_IsAcknowledged_CreatedOnUtc",
                schema: "commerce",
                table: "OperationalAlerts",
                columns: new[] { "IsAcknowledged", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAuditLogs_OccurredOnUtc",
                schema: "commerce",
                table: "OperationalAuditLogs",
                column: "OccurredOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobs_CreatedOnUtc",
                schema: "commerce",
                table: "OperationalJobs",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobs_JobType",
                schema: "commerce",
                table: "OperationalJobs",
                column: "JobType");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobs_Status_NextVisibleOnUtc_Priority",
                schema: "commerce",
                table: "OperationalJobs",
                columns: new[] { "Status", "NextVisibleOnUtc", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiRequestLogs",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "OperationalAlerts",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "OperationalAuditLogs",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "OperationalJobs",
                schema: "commerce");
        }
    }
}
