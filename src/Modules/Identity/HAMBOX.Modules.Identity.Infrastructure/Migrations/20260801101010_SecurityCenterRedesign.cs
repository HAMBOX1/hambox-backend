using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HAMBOX.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SecurityCenterRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "identity",
                table: "UserSessions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                schema: "identity",
                table: "UserSessions",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                schema: "identity",
                table: "UserSessions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsName",
                schema: "identity",
                table: "UserSessions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcknowledgedByUserId",
                schema: "identity",
                table: "SecurityEventLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AcknowledgedOnUtc",
                schema: "identity",
                table: "SecurityEventLogs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "identity",
                table: "SecurityEventLogs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNotes",
                schema: "identity",
                table: "SecurityEventLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedByUserId",
                schema: "identity",
                table: "SecurityEventLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedOnUtc",
                schema: "identity",
                table: "SecurityEventLogs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "identity",
                table: "SecurityEventLogs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.AddColumn<string>(
                name: "BrowserName",
                schema: "identity",
                table: "LoginHistory",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "identity",
                table: "LoginHistory",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                schema: "identity",
                table: "LoginHistory",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                schema: "identity",
                table: "LoginHistory",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                schema: "identity",
                table: "LoginHistory",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsName",
                schema: "identity",
                table: "LoginHistory",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                schema: "identity",
                table: "LoginHistory",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrustedDevices",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BrowserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OsName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FirstSeenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    LastCountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    LastCity = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LoginCount = table.Column<int>(type: "int", nullable: false),
                    IsTrusted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TrustedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TrustedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsBlocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    BlockedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BlockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BlockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustedDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustedDevices_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "Permissions",
                columns: new[] { "Id", "CreatedOnUtc", "Description", "GroupId", "ModifiedOnUtc", "Name", "NormalizedName", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000269"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000029"), null, "Security.ManageAlerts", "SECURITY.MANAGEALERTS", 7 },
                    { new Guid("20000000-0000-0000-0000-000000000270"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000029"), null, "Security.ManageDevices", "SECURITY.MANAGEDEVICES", 8 }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "RolePermissions",
                columns: new[] { "Id", "CreatedOnUtc", "ModifiedOnUtc", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000129"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000269"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000130"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000270"), new Guid("10000000-0000-0000-0000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEventLogs_Status_Severity",
                schema: "identity",
                table: "SecurityEventLogs",
                columns: new[] { "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistory_Fingerprint",
                schema: "identity",
                table: "LoginHistory",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevices_Fingerprint",
                schema: "identity",
                table: "TrustedDevices",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevices_UserId_Fingerprint",
                schema: "identity",
                table: "TrustedDevices",
                columns: new[] { "UserId", "Fingerprint" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrustedDevices",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_SecurityEventLogs_Status_Severity",
                schema: "identity",
                table: "SecurityEventLogs");

            migrationBuilder.DropIndex(
                name: "IX_LoginHistory_Fingerprint",
                schema: "identity",
                table: "LoginHistory");

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000269"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000270"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000129"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000130"));

            migrationBuilder.DropColumn(
                name: "City",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "Fingerprint",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "OsName",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "AcknowledgedByUserId",
                schema: "identity",
                table: "SecurityEventLogs");

            migrationBuilder.DropColumn(
                name: "AcknowledgedOnUtc",
                schema: "identity",
                table: "SecurityEventLogs");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "identity",
                table: "SecurityEventLogs");

            migrationBuilder.DropColumn(
                name: "ResolutionNotes",
                schema: "identity",
                table: "SecurityEventLogs");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                schema: "identity",
                table: "SecurityEventLogs");

            migrationBuilder.DropColumn(
                name: "ResolvedOnUtc",
                schema: "identity",
                table: "SecurityEventLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "identity",
                table: "SecurityEventLogs");

            migrationBuilder.DropColumn(
                name: "BrowserName",
                schema: "identity",
                table: "LoginHistory");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "identity",
                table: "LoginHistory");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                schema: "identity",
                table: "LoginHistory");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                schema: "identity",
                table: "LoginHistory");

            migrationBuilder.DropColumn(
                name: "Fingerprint",
                schema: "identity",
                table: "LoginHistory");

            migrationBuilder.DropColumn(
                name: "OsName",
                schema: "identity",
                table: "LoginHistory");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                schema: "identity",
                table: "LoginHistory");
        }
    }
}
