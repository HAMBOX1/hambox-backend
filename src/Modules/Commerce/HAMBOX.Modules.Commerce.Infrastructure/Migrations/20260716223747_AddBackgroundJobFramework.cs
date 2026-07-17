using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundJobFramework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent",
                schema: "commerce",
                table: "OperationalJobs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Queue",
                schema: "commerce",
                table: "OperationalJobs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.CreateTable(
                name: "BackgroundJobExecutionHistory",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    Exception = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    WorkerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobExecutionHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobs_Queue",
                schema: "commerce",
                table: "OperationalJobs",
                column: "Queue");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobExecutionHistory_JobId_AttemptNumber",
                schema: "commerce",
                table: "BackgroundJobExecutionHistory",
                columns: new[] { "JobId", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobExecutionHistory_StartedOnUtc",
                schema: "commerce",
                table: "BackgroundJobExecutionHistory",
                column: "StartedOnUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackgroundJobExecutionHistory",
                schema: "commerce");

            migrationBuilder.DropIndex(
                name: "IX_OperationalJobs_Queue",
                schema: "commerce",
                table: "OperationalJobs");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                schema: "commerce",
                table: "OperationalJobs");

            migrationBuilder.DropColumn(
                name: "Queue",
                schema: "commerce",
                table: "OperationalJobs");
        }
    }
}
