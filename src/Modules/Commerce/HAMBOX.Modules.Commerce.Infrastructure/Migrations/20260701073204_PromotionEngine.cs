using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PromotionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppliedCouponCode",
                schema: "commerce",
                table: "ShoppingCarts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderAppliedPromotions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CouponCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PromotionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PromotionType = table.Column<int>(type: "int", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CouponCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAppliedPromotions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromotionAuditLogs",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CouponCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromotionRedemptions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CouponCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PromotionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RedeemedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionRedemptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DiscountType = table.Column<int>(type: "int", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalRedemptions = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CouponCodes",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsSingleUse = table.Column<bool>(type: "bit", nullable: false),
                    MaxUses = table.Column<int>(type: "int", nullable: true),
                    PerUserMaxUses = table.Column<int>(type: "int", nullable: true),
                    UsedCount = table.Column<int>(type: "int", nullable: false),
                    ExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CouponCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CouponCodes_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalSchema: "commerce",
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromotionConditions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConditionType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionConditions_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalSchema: "commerce",
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromotionTargets",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionTargets_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalSchema: "commerce",
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CouponCodes_Code",
                schema: "commerce",
                table: "CouponCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CouponCodes_PromotionId",
                schema: "commerce",
                table: "CouponCodes",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAppliedPromotions_OrderId",
                schema: "commerce",
                table: "OrderAppliedPromotions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionAuditLogs_OccurredOnUtc",
                schema: "commerce",
                table: "PromotionAuditLogs",
                column: "OccurredOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionAuditLogs_PromotionId",
                schema: "commerce",
                table: "PromotionAuditLogs",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionConditions_PromotionId_Type",
                schema: "commerce",
                table: "PromotionConditions",
                columns: new[] { "PromotionId", "ConditionType" });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRedemptions_OrderId",
                schema: "commerce",
                table: "PromotionRedemptions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRedemptions_PromotionId",
                schema: "commerce",
                table: "PromotionRedemptions",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRedemptions_UserId",
                schema: "commerce",
                table: "PromotionRedemptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_Status_Published_Type",
                schema: "commerce",
                table: "Promotions",
                columns: new[] { "Status", "IsPublished", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionTargets_Promotion_Target",
                schema: "commerce",
                table: "PromotionTargets",
                columns: new[] { "PromotionId", "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CouponCodes",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "OrderAppliedPromotions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "PromotionAuditLogs",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "PromotionConditions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "PromotionRedemptions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "PromotionTargets",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "Promotions",
                schema: "commerce");

            migrationBuilder.DropColumn(
                name: "AppliedCouponCode",
                schema: "commerce",
                table: "ShoppingCarts");
        }
    }
}
