using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StarsCampaignAudience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxStarsPerOrder",
                schema: "commerce",
                table: "StarsEarnRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinRankKey",
                schema: "commerce",
                table: "StarsEarnRules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresElite",
                schema: "commerce",
                table: "StarsEarnRules",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxStarsPerOrder",
                schema: "commerce",
                table: "StarsEarnRules");

            migrationBuilder.DropColumn(
                name: "MinRankKey",
                schema: "commerce",
                table: "StarsEarnRules");

            migrationBuilder.DropColumn(
                name: "RequiresElite",
                schema: "commerce",
                table: "StarsEarnRules");
        }
    }
}
