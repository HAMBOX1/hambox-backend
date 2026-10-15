using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralReviewHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistrationIp",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewApprovedByUserId",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewApprovedOnUtc",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralHistoryEntries_ReferrerUserId_RegistrationIp",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                columns: new[] { "ReferrerUserId", "RegistrationIp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReferralHistoryEntries_ReferrerUserId_RegistrationIp",
                schema: "commerce",
                table: "ReferralHistoryEntries");

            migrationBuilder.DropColumn(
                name: "RegistrationIp",
                schema: "commerce",
                table: "ReferralHistoryEntries");

            migrationBuilder.DropColumn(
                name: "ReviewApprovedByUserId",
                schema: "commerce",
                table: "ReferralHistoryEntries");

            migrationBuilder.DropColumn(
                name: "ReviewApprovedOnUtc",
                schema: "commerce",
                table: "ReferralHistoryEntries");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                schema: "commerce",
                table: "ReferralHistoryEntries");
        }
    }
}
