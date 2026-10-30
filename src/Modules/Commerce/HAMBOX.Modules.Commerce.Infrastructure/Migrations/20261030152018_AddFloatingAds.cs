using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFloatingAds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FloatingAds",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MessageEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MessageAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ButtonLabelEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ButtonLabelAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LinkUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Audience = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartsOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndsOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    DismissHours = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloatingAds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FloatingAds_IsActive_SortOrder",
                schema: "commerce",
                table: "FloatingAds",
                columns: new[] { "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FloatingAds",
                schema: "commerce");
        }
    }
}
