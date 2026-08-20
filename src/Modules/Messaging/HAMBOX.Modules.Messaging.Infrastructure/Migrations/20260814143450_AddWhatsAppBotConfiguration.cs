using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppBotConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppBotConfigAuditLogs",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Target = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppBotConfigAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppBotConfigurations",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WelcomeMessageEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WelcomeMessageAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FallbackMessageEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FallbackMessageAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppBotConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppMenuItems",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    LabelEn = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LabelAr = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppMenuItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBotConfigAuditLogs_CreatedOnUtc",
                schema: "messaging",
                table: "WhatsAppBotConfigAuditLogs",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMenuItems_Action",
                schema: "messaging",
                table: "WhatsAppMenuItems",
                column: "Action",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppBotConfigAuditLogs",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "WhatsAppBotConfigurations",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "WhatsAppMenuItems",
                schema: "messaging");
        }
    }
}
