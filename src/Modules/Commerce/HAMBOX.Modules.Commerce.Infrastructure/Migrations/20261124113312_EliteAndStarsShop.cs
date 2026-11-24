using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EliteAndStarsShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMonths",
                schema: "commerce",
                table: "MembershipPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MembershipCodeRedemptions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RedeemedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipCodeRedemptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipCodes",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantDays = table.Column<int>(type: "int", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "int", nullable: false),
                    RedeemedCount = table.Column<int>(type: "int", nullable: false),
                    ExpiresOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RewardItems",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StarsPrice = table.Column<int>(type: "int", nullable: false),
                    RemainingQuantity = table.Column<int>(type: "int", nullable: true),
                    MaxPerUser = table.Column<int>(type: "int", nullable: true),
                    MinRankKey = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RequiresElite = table.Column<bool>(type: "bit", nullable: false),
                    IsGiftOption = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartsOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndsOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RewardRedemptions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RewardItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StarsSpent = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RedeemedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardRedemptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StarsGoals",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RewardItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarsGoals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MembershipCodeRedemptions_Code_User",
                schema: "commerce",
                table: "MembershipCodeRedemptions",
                columns: new[] { "CodeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MembershipCodes_Code",
                schema: "commerce",
                table: "MembershipCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardItems_Active_Sort",
                schema: "commerce",
                table: "RewardItems",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardItems_Variant",
                schema: "commerce",
                table: "RewardItems",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_Order",
                schema: "commerce",
                table: "RewardRedemptions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_User_Reward",
                schema: "commerce",
                table: "RewardRedemptions",
                columns: new[] { "UserId", "RewardItemId" });

            migrationBuilder.CreateIndex(
                name: "UX_RewardRedemptions_User_Period",
                schema: "commerce",
                table: "RewardRedemptions",
                columns: new[] { "UserId", "PeriodKey" },
                unique: true,
                filter: "[PeriodKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_StarsGoals_User_Reward",
                schema: "commerce",
                table: "StarsGoals",
                columns: new[] { "UserId", "RewardItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MembershipCodeRedemptions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "MembershipCodes",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "RewardItems",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "RewardRedemptions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "StarsGoals",
                schema: "commerce");

            migrationBuilder.DropColumn(
                name: "DurationMonths",
                schema: "commerce",
                table: "MembershipPlans");
        }
    }
}
