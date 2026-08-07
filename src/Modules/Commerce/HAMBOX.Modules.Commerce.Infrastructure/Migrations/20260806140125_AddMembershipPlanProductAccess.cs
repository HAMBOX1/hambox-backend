using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipPlanProductAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MembershipPlanProductAccess",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipPlanProductAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipPlanProductAccess_MembershipPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "commerce",
                        principalTable: "MembershipPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipPlanProductAccess_PlanId_ProductId",
                schema: "commerce",
                table: "MembershipPlanProductAccess",
                columns: new[] { "PlanId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipPlanProductAccess_ProductId",
                schema: "commerce",
                table: "MembershipPlanProductAccess",
                column: "ProductId");

            // Badge is no longer a configurable MembershipBenefit — MembershipPlan.BadgeLabel
            // already carries the same information for every seeded plan, so these rows are
            // redundant. BenefitType 6 = Badge (see MembershipBenefitType enum; never renumber it).
            migrationBuilder.Sql("DELETE FROM [commerce].[MembershipBenefits] WHERE [BenefitType] = 6;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deleted Badge benefit rows are not restored — BadgeLabel on the plan already
            // preserves the information they carried.
            migrationBuilder.DropTable(
                name: "MembershipPlanProductAccess",
                schema: "commerce");
        }
    }
}
