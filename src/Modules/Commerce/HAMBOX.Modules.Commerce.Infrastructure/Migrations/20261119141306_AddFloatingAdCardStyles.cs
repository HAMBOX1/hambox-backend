using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFloatingAdCardStyles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OldPriceText",
                schema: "commerce",
                table: "FloatingAds",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pages",
                schema: "commerce",
                table: "FloatingAds",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionDesktop",
                schema: "commerce",
                table: "FloatingAds",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "BottomLeft");

            migrationBuilder.AddColumn<string>(
                name: "PositionMobile",
                schema: "commerce",
                table: "FloatingAds",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Bottom");

            migrationBuilder.AddColumn<string>(
                name: "PriceText",
                schema: "commerce",
                table: "FloatingAds",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Style",
                schema: "commerce",
                table: "FloatingAds",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Mini");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OldPriceText",
                schema: "commerce",
                table: "FloatingAds");

            migrationBuilder.DropColumn(
                name: "Pages",
                schema: "commerce",
                table: "FloatingAds");

            migrationBuilder.DropColumn(
                name: "PositionDesktop",
                schema: "commerce",
                table: "FloatingAds");

            migrationBuilder.DropColumn(
                name: "PositionMobile",
                schema: "commerce",
                table: "FloatingAds");

            migrationBuilder.DropColumn(
                name: "PriceText",
                schema: "commerce",
                table: "FloatingAds");

            migrationBuilder.DropColumn(
                name: "Style",
                schema: "commerce",
                table: "FloatingAds");
        }
    }
}
