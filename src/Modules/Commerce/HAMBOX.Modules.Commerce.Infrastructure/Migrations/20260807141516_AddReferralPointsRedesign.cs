using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralPointsRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "QualifiedOnUtc",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferredEmail",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RewardedOnUtc",
                schema: "commerce",
                table: "ReferralHistoryEntries",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QualifiedOnUtc",
                schema: "commerce",
                table: "ReferralHistoryEntries");

            migrationBuilder.DropColumn(
                name: "ReferredEmail",
                schema: "commerce",
                table: "ReferralHistoryEntries");

            migrationBuilder.DropColumn(
                name: "RewardedOnUtc",
                schema: "commerce",
                table: "ReferralHistoryEntries");
        }
    }
}
