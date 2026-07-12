using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdminAuthenticationSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthContext",
                schema: "identity",
                table: "UserSessions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "customer");

            migrationBuilder.AddColumn<string>(
                name: "BrowserName",
                schema: "identity",
                table: "UserSessions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                schema: "identity",
                table: "UserSessions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefreshTokenId",
                schema: "identity",
                table: "UserSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthContext",
                schema: "identity",
                table: "RefreshTokens",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "customer");

            migrationBuilder.AddColumn<string>(
                name: "ReplacedByTokenHash",
                schema: "identity",
                table: "RefreshTokens",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                schema: "identity",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "AdminLoginChallenges",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastResendOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminLoginChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminLoginChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminOtpAuditLogs",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    OccurredOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminOtpAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminOtpAuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminLoginChallenges_ExpiresOnUtc",
                schema: "identity",
                table: "AdminLoginChallenges",
                column: "ExpiresOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdminLoginChallenges_UserId",
                schema: "identity",
                table: "AdminLoginChallenges",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminOtpAuditLogs_ChallengeId",
                schema: "identity",
                table: "AdminOtpAuditLogs",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminOtpAuditLogs_OccurredOnUtc",
                schema: "identity",
                table: "AdminOtpAuditLogs",
                column: "OccurredOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdminOtpAuditLogs_UserId",
                schema: "identity",
                table: "AdminOtpAuditLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminLoginChallenges",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AdminOtpAuditLogs",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "AuthContext",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "BrowserName",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "DeviceName",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "RefreshTokenId",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "AuthContext",
                schema: "identity",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ReplacedByTokenHash",
                schema: "identity",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "identity",
                table: "RefreshTokens");
        }
    }
}
