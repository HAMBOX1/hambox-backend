using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationArchiveSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedOnUtc",
                schema: "commerce",
                table: "UserNotifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "commerce",
                table: "UserNotifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "commerce",
                table: "UserNotifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_IsArchived",
                schema: "commerce",
                table: "UserNotifications",
                columns: new[] { "UserId", "IsArchived" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_UserId_IsArchived",
                schema: "commerce",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                schema: "commerce",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                schema: "commerce",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "commerce",
                table: "UserNotifications");
        }
    }
}
