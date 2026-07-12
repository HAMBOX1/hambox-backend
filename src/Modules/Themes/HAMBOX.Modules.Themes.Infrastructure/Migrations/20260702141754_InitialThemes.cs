using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Themes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "StoreThemes",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BaseMode = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    PublishedVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersionCounter = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreThemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThemeAuditLogs",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemeAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThemePreviewSessions",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemePreviewSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThemeAssets",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetType = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemeAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThemeAssets_StoreThemes_ThemeId",
                        column: x => x.ThemeId,
                        principalSchema: "platform",
                        principalTable: "StoreThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThemeAssignments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentType = table.Column<int>(type: "int", nullable: false),
                    TargetKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemeAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThemeAssignments_StoreThemes_ThemeId",
                        column: x => x.ThemeId,
                        principalSchema: "platform",
                        principalTable: "StoreThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThemeSchedules",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecurrenceRule = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemeSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThemeSchedules_StoreThemes_ThemeId",
                        column: x => x.ThemeId,
                        principalSchema: "platform",
                        principalTable: "StoreThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThemeVersions",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    TokensJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ValidationWarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThemeVersions_StoreThemes_ThemeId",
                        column: x => x.ThemeId,
                        principalSchema: "platform",
                        principalTable: "StoreThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreThemes_Slug",
                schema: "platform",
                table: "StoreThemes",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThemeAssets_ThemeId_AssetType",
                schema: "platform",
                table: "ThemeAssets",
                columns: new[] { "ThemeId", "AssetType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThemeAssignments_AssignmentType_TargetKey_IsActive",
                schema: "platform",
                table: "ThemeAssignments",
                columns: new[] { "AssignmentType", "TargetKey", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ThemeAssignments_ThemeId",
                schema: "platform",
                table: "ThemeAssignments",
                column: "ThemeId");

            migrationBuilder.CreateIndex(
                name: "IX_ThemeAuditLogs_ThemeId_CreatedOnUtc",
                schema: "platform",
                table: "ThemeAuditLogs",
                columns: new[] { "ThemeId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ThemePreviewSessions_Token",
                schema: "platform",
                table: "ThemePreviewSessions",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThemeSchedules_ThemeId",
                schema: "platform",
                table: "ThemeSchedules",
                column: "ThemeId");

            migrationBuilder.CreateIndex(
                name: "IX_ThemeVersions_ThemeId_VersionNumber",
                schema: "platform",
                table: "ThemeVersions",
                columns: new[] { "ThemeId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThemeAssets",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ThemeAssignments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ThemeAuditLogs",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ThemePreviewSessions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ThemeSchedules",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "ThemeVersions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "StoreThemes",
                schema: "platform");
        }
    }
}
