using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "WhatsAppConversationSessions",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentMenu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SelectedCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContextJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CustomerUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PendingVerificationEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PendingVerificationCodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PendingVerificationExpiresOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PendingVerificationAttempts = table.Column<int>(type: "int", nullable: false),
                    ExpiresOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppConversationSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversationSessions_PhoneNumber",
                schema: "messaging",
                table: "WhatsAppConversationSessions",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppConversationSessions",
                schema: "messaging");
        }
    }
}
