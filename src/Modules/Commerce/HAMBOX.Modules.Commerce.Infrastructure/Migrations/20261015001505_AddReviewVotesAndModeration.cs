using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewVotesAndModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HiddenByUserId",
                schema: "commerce",
                table: "ProductReviews",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HiddenOnUtc",
                schema: "commerce",
                table: "ProductReviews",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenReason",
                schema: "commerce",
                table: "ProductReviews",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                schema: "commerce",
                table: "ProductReviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ReviewVotes",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsHelpful = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewVotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewVotes_ReviewId_UserId",
                schema: "commerce",
                table: "ReviewVotes",
                columns: new[] { "ReviewId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewVotes",
                schema: "commerce");

            migrationBuilder.DropColumn(
                name: "HiddenByUserId",
                schema: "commerce",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "HiddenOnUtc",
                schema: "commerce",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "HiddenReason",
                schema: "commerce",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                schema: "commerce",
                table: "ProductReviews");
        }
    }
}
