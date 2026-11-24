using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StarsCampaignsCouponsCosmetics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnnouncedOnUtc",
                schema: "commerce",
                table: "StarsEarnRules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BudgetStars",
                schema: "commerce",
                table: "StarsEarnRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Condition",
                schema: "commerce",
                table: "StarsEarnRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConditionProductIds",
                schema: "commerce",
                table: "StarsEarnRules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConditionValue",
                schema: "commerce",
                table: "StarsEarnRules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MaxStarsPerCustomer",
                schema: "commerce",
                table: "StarsEarnRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CouponCode",
                schema: "commerce",
                table: "RewardRedemptions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CouponPromotionId",
                schema: "commerce",
                table: "RewardItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CouponValidDays",
                schema: "commerce",
                table: "RewardItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SaleEndsOnUtc",
                schema: "commerce",
                table: "RewardItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaleStarsPrice",
                schema: "commerce",
                table: "RewardItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SaleStartsOnUtc",
                schema: "commerce",
                table: "RewardItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleAr",
                schema: "commerce",
                table: "RewardItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEn",
                schema: "commerce",
                table: "RewardItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiryNotifiedOnUtc",
                schema: "commerce",
                table: "PointsTransactions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerCosmetics",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CosmeticId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcquiredOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCosmetics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerProfileStyles",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FrameId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BadgeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AnimationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProfileStyles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberPriceUsages",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPriceUsages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfileCosmetics",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AccentColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Unlock = table.Column<int>(type: "int", nullable: false),
                    UnlockRankKey = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    UnlockMissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StarsPrice = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileCosmetics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StarsRuleAwards",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stars = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarsRuleAwards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_CustomerCosmetics_User_Cosmetic",
                schema: "commerce",
                table: "CustomerCosmetics",
                columns: new[] { "UserId", "CosmeticId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CustomerProfileStyles_UserId",
                schema: "commerce",
                table: "CustomerProfileStyles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberPriceUsages_User_Period",
                schema: "commerce",
                table: "MemberPriceUsages",
                columns: new[] { "UserId", "PeriodKey" });

            migrationBuilder.CreateIndex(
                name: "UX_MemberPriceUsages_Order_Variant",
                schema: "commerce",
                table: "MemberPriceUsages",
                columns: new[] { "OrderId", "ProductVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProfileCosmetics_Key",
                schema: "commerce",
                table: "ProfileCosmetics",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StarsRuleAwards_Rule_User",
                schema: "commerce",
                table: "StarsRuleAwards",
                columns: new[] { "RuleId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "UX_StarsRuleAwards_Rule_Order",
                schema: "commerce",
                table: "StarsRuleAwards",
                columns: new[] { "RuleId", "OrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerCosmetics",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "CustomerProfileStyles",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "MemberPriceUsages",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "ProfileCosmetics",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "StarsRuleAwards",
                schema: "commerce");

            migrationBuilder.DropColumn(
                name: "AnnouncedOnUtc",
                schema: "commerce",
                table: "StarsEarnRules");

            migrationBuilder.DropColumn(
                name: "BudgetStars",
                schema: "commerce",
                table: "StarsEarnRules");

            migrationBuilder.DropColumn(
                name: "Condition",
                schema: "commerce",
                table: "StarsEarnRules");

            migrationBuilder.DropColumn(
                name: "ConditionProductIds",
                schema: "commerce",
                table: "StarsEarnRules");

            migrationBuilder.DropColumn(
                name: "ConditionValue",
                schema: "commerce",
                table: "StarsEarnRules");

            migrationBuilder.DropColumn(
                name: "MaxStarsPerCustomer",
                schema: "commerce",
                table: "StarsEarnRules");

            migrationBuilder.DropColumn(
                name: "CouponCode",
                schema: "commerce",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "CouponPromotionId",
                schema: "commerce",
                table: "RewardItems");

            migrationBuilder.DropColumn(
                name: "CouponValidDays",
                schema: "commerce",
                table: "RewardItems");

            migrationBuilder.DropColumn(
                name: "SaleEndsOnUtc",
                schema: "commerce",
                table: "RewardItems");

            migrationBuilder.DropColumn(
                name: "SaleStarsPrice",
                schema: "commerce",
                table: "RewardItems");

            migrationBuilder.DropColumn(
                name: "SaleStartsOnUtc",
                schema: "commerce",
                table: "RewardItems");

            migrationBuilder.DropColumn(
                name: "TitleAr",
                schema: "commerce",
                table: "RewardItems");

            migrationBuilder.DropColumn(
                name: "TitleEn",
                schema: "commerce",
                table: "RewardItems");

            migrationBuilder.DropColumn(
                name: "ExpiryNotifiedOnUtc",
                schema: "commerce",
                table: "PointsTransactions");
        }
    }
}
