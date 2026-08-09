using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Content.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPageScopeToLandingPageTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftSeoDescription",
                schema: "content",
                table: "LandingPageTemplates",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftSeoOgImageUrl",
                schema: "content",
                table: "LandingPageTemplates",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftSeoTitle",
                schema: "content",
                table: "LandingPageTemplates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                schema: "content",
                table: "LandingPageTemplates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SeoDescription",
                schema: "content",
                table: "LandingPageTemplates",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoOgImageUrl",
                schema: "content",
                table: "LandingPageTemplates",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoTitle",
                schema: "content",
                table: "LandingPageTemplates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetId",
                schema: "content",
                table: "LandingPageTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandingPageTemplates_ActiveTarget",
                schema: "content",
                table: "LandingPageTemplates",
                columns: new[] { "Scope", "TargetId" },
                unique: true,
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LandingPageTemplates_ActiveTarget",
                schema: "content",
                table: "LandingPageTemplates");

            migrationBuilder.DropColumn(
                name: "DraftSeoDescription",
                schema: "content",
                table: "LandingPageTemplates");

            migrationBuilder.DropColumn(
                name: "DraftSeoOgImageUrl",
                schema: "content",
                table: "LandingPageTemplates");

            migrationBuilder.DropColumn(
                name: "DraftSeoTitle",
                schema: "content",
                table: "LandingPageTemplates");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "content",
                table: "LandingPageTemplates");

            migrationBuilder.DropColumn(
                name: "SeoDescription",
                schema: "content",
                table: "LandingPageTemplates");

            migrationBuilder.DropColumn(
                name: "SeoOgImageUrl",
                schema: "content",
                table: "LandingPageTemplates");

            migrationBuilder.DropColumn(
                name: "SeoTitle",
                schema: "content",
                table: "LandingPageTemplates");

            migrationBuilder.DropColumn(
                name: "TargetId",
                schema: "content",
                table: "LandingPageTemplates");
        }
    }
}
