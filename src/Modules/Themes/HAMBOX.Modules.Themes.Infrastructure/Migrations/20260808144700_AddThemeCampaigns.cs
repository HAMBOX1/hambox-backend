using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Themes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignAuditLogs",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThemeCampaigns",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ThemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemeCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThemeCampaigns_StoreThemes_ThemeId",
                        column: x => x.ThemeId,
                        principalSchema: "platform",
                        principalTable: "StoreThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAuditLogs_CampaignId_CreatedOnUtc",
                schema: "platform",
                table: "CampaignAuditLogs",
                columns: new[] { "CampaignId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ThemeCampaigns_Status_IsEnabled_StartsAtUtc_EndsAtUtc",
                schema: "platform",
                table: "ThemeCampaigns",
                columns: new[] { "Status", "IsEnabled", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ThemeCampaigns_ThemeId",
                schema: "platform",
                table: "ThemeCampaigns",
                column: "ThemeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignAuditLogs",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ThemeCampaigns",
                schema: "platform");
        }
    }
}
