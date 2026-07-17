using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Legal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorLegalSectionsCms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Rename existing tables/columns in place — preserves all rows and version history ──
            migrationBuilder.RenameTable(name: "LegalDocuments", schema: "platform", newName: "LegalSections", newSchema: "platform");
            migrationBuilder.RenameTable(name: "LegalDocumentVersions", schema: "platform", newName: "LegalSectionVersions", newSchema: "platform");
            migrationBuilder.RenameTable(name: "LegalDocumentAuditLogs", schema: "platform", newName: "LegalSectionAuditLogs", newSchema: "platform");

            migrationBuilder.RenameColumn(name: "LegalDocumentId", schema: "platform", table: "LegalSectionVersions", newName: "LegalSectionId");
            migrationBuilder.RenameColumn(name: "LegalDocumentId", schema: "platform", table: "LegalSectionAuditLogs", newName: "LegalSectionId");

            migrationBuilder.RenameIndex(name: "IX_LegalDocumentVersions_LegalDocumentId_VersionNumber", schema: "platform", table: "LegalSectionVersions", newName: "IX_LegalSectionVersions_LegalSectionId_VersionNumber");
            migrationBuilder.RenameIndex(name: "IX_LegalDocumentAuditLogs_LegalDocumentId_CreatedOnUtc", schema: "platform", table: "LegalSectionAuditLogs", newName: "IX_LegalSectionAuditLogs_LegalSectionId_CreatedOnUtc");

            // ── New CMS metadata columns on LegalSections ──
            migrationBuilder.AddColumn<string>(name: "Slug", schema: "platform", table: "LegalSections", type: "nvarchar(150)", maxLength: 150, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Category", schema: "platform", table: "LegalSections", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Icon", schema: "platform", table: "LegalSections", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<int>(name: "SortOrder", schema: "platform", table: "LegalSections", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "DescriptionEn", schema: "platform", table: "LegalSections", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(name: "DescriptionAr", schema: "platform", table: "LegalSections", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(name: "SeoTitle", schema: "platform", table: "LegalSections", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "SeoDescription", schema: "platform", table: "LegalSections", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<string>(name: "SeoKeywords", schema: "platform", table: "LegalSections", type: "nvarchar(300)", maxLength: 300, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "ShowInFooter", schema: "platform", table: "LegalSections", type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(name: "ShowInNavigation", schema: "platform", table: "LegalSections", type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(name: "RequireAcceptance", schema: "platform", table: "LegalSections", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "IsArchived", schema: "platform", table: "LegalSections", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "CreatedBy", schema: "platform", table: "LegalSections", type: "nvarchar(128)", maxLength: 128, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ModifiedBy", schema: "platform", table: "LegalSections", type: "nvarchar(128)", maxLength: 128, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "IsDeleted", schema: "platform", table: "LegalSections", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "DeletedOnUtc", schema: "platform", table: "LegalSections", type: "datetimeoffset", nullable: true);

            migrationBuilder.AddColumn<string>(name: "VersionNotes", schema: "platform", table: "LegalSectionVersions", type: "nvarchar(1000)", maxLength: 1000, nullable: true);

            // ── Backfill Slug/Category/Icon/RequireAcceptance from the old fixed Type enum, using the
            //    exact slugs the frontend already hardcodes today — this is what keeps existing
            //    /legal/{slug} URLs resolving to the same content after the refactor. ──
            migrationBuilder.Sql(@"
                UPDATE [platform].[LegalSections] SET
                    [Slug] = CASE [Type]
                        WHEN 0 THEN 'terms' WHEN 1 THEN 'privacy' WHEN 2 THEN 'refund'
                        WHEN 3 THEN 'delivery' WHEN 4 THEN 'cookie' WHEN 5 THEN 'about'
                        WHEN 6 THEN 'contact' ELSE CONCAT('section-', [Id]) END,
                    [Category] = CASE [Type]
                        WHEN 3 THEN 'Commerce'
                        WHEN 5 THEN 'General' WHEN 6 THEN 'General'
                        ELSE 'Legal' END,
                    [Icon] = CASE [Type]
                        WHEN 0 THEN 'pi pi-file-check' WHEN 1 THEN 'pi pi-shield' WHEN 2 THEN 'pi pi-replay'
                        WHEN 3 THEN 'pi pi-bolt' WHEN 5 THEN 'pi pi-info-circle' WHEN 6 THEN 'pi pi-envelope'
                        ELSE 'pi pi-info-circle' END,
                    [RequireAcceptance] = CASE WHEN [Type] IN (0, 1, 2) THEN 1 ELSE 0 END;
            ");

            migrationBuilder.AlterColumn<string>(name: "Slug", schema: "platform", table: "LegalSections", type: "nvarchar(150)", maxLength: 150, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(150)", oldMaxLength: 150, oldNullable: true);

            migrationBuilder.DropIndex(name: "IX_LegalDocuments_Type", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "Type", schema: "platform", table: "LegalSections");

            migrationBuilder.CreateIndex(name: "IX_LegalSections_Slug", schema: "platform", table: "LegalSections", column: "Slug", unique: true, filter: "[IsDeleted] = 0");
            migrationBuilder.CreateIndex(name: "IX_LegalSections_IsDeleted", schema: "platform", table: "LegalSections", column: "IsDeleted", filter: "[IsDeleted] = 0");

            // ── New normalized acceptance table, replacing the fixed Terms/Privacy/Refund columns ──
            migrationBuilder.CreateTable(
                name: "LegalSectionAcceptances",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LegalSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalSectionAcceptances", x => x.Id);
                });

            // ── Copy every historical Terms/Privacy/Refund acceptance into the new normalized shape ──
            migrationBuilder.Sql(@"
                INSERT INTO [platform].[LegalSectionAcceptances]
                    ([Id], [UserId], [LegalSectionId], [VersionNumber], [AcceptedAtUtc], [IpAddress], [UserAgent], [Language], [CreatedOnUtc])
                SELECT NEWID(), a.[UserId], s.[Id], a.[TermsVersion], a.[AcceptedAtUtc], a.[IpAddress], a.[UserAgent], a.[Language], a.[CreatedOnUtc]
                FROM [platform].[LegalAcceptances] a
                JOIN [platform].[LegalSections] s ON s.[Slug] = 'terms'
                WHERE a.[TermsVersion] > 0
                UNION ALL
                SELECT NEWID(), a.[UserId], s.[Id], a.[PrivacyVersion], a.[AcceptedAtUtc], a.[IpAddress], a.[UserAgent], a.[Language], a.[CreatedOnUtc]
                FROM [platform].[LegalAcceptances] a
                JOIN [platform].[LegalSections] s ON s.[Slug] = 'privacy'
                WHERE a.[PrivacyVersion] > 0
                UNION ALL
                SELECT NEWID(), a.[UserId], s.[Id], a.[RefundVersion], a.[AcceptedAtUtc], a.[IpAddress], a.[UserAgent], a.[Language], a.[CreatedOnUtc]
                FROM [platform].[LegalAcceptances] a
                JOIN [platform].[LegalSections] s ON s.[Slug] = 'refund'
                WHERE a.[RefundVersion] > 0;
            ");

            migrationBuilder.DropTable(name: "LegalAcceptances", schema: "platform");

            migrationBuilder.CreateIndex(
                name: "IX_LegalSectionAcceptances_UserId_LegalSectionId_AcceptedAtUtc",
                schema: "platform",
                table: "LegalSectionAcceptances",
                columns: new[] { "UserId", "LegalSectionId", "AcceptedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LegalSectionAcceptances", schema: "platform");

            migrationBuilder.AddColumn<int>(name: "Type", schema: "platform", table: "LegalSections", type: "int", nullable: false, defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE [platform].[LegalSections] SET [Type] = CASE [Slug]
                    WHEN 'terms' THEN 0 WHEN 'privacy' THEN 1 WHEN 'refund' THEN 2
                    WHEN 'delivery' THEN 3 WHEN 'cookie' THEN 4 WHEN 'about' THEN 5
                    WHEN 'contact' THEN 6 ELSE 0 END;
            ");

            migrationBuilder.DropIndex(name: "IX_LegalSections_Slug", schema: "platform", table: "LegalSections");
            migrationBuilder.DropIndex(name: "IX_LegalSections_IsDeleted", schema: "platform", table: "LegalSections");

            migrationBuilder.DropColumn(name: "Slug", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "Category", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "Icon", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "SortOrder", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "DescriptionEn", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "DescriptionAr", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "SeoTitle", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "SeoDescription", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "SeoKeywords", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "ShowInFooter", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "ShowInNavigation", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "RequireAcceptance", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "IsArchived", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "CreatedBy", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "ModifiedBy", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "IsDeleted", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "DeletedOnUtc", schema: "platform", table: "LegalSections");
            migrationBuilder.DropColumn(name: "VersionNotes", schema: "platform", table: "LegalSectionVersions");

            migrationBuilder.CreateIndex(name: "IX_LegalDocuments_Type", schema: "platform", table: "LegalSections", column: "Type", unique: true);

            migrationBuilder.RenameIndex(name: "IX_LegalSectionVersions_LegalSectionId_VersionNumber", schema: "platform", table: "LegalSectionVersions", newName: "IX_LegalDocumentVersions_LegalDocumentId_VersionNumber");
            migrationBuilder.RenameIndex(name: "IX_LegalSectionAuditLogs_LegalSectionId_CreatedOnUtc", schema: "platform", table: "LegalSectionAuditLogs", newName: "IX_LegalDocumentAuditLogs_LegalDocumentId_CreatedOnUtc");

            migrationBuilder.RenameColumn(name: "LegalSectionId", schema: "platform", table: "LegalSectionVersions", newName: "LegalDocumentId");
            migrationBuilder.RenameColumn(name: "LegalSectionId", schema: "platform", table: "LegalSectionAuditLogs", newName: "LegalDocumentId");

            migrationBuilder.RenameTable(name: "LegalSections", schema: "platform", newName: "LegalDocuments", newSchema: "platform");
            migrationBuilder.RenameTable(name: "LegalSectionVersions", schema: "platform", newName: "LegalDocumentVersions", newSchema: "platform");
            migrationBuilder.RenameTable(name: "LegalSectionAuditLogs", schema: "platform", newName: "LegalDocumentAuditLogs", newSchema: "platform");

            migrationBuilder.CreateTable(
                name: "LegalAcceptances",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PrivacyVersion = table.Column<int>(type: "int", nullable: false),
                    RefundVersion = table.Column<int>(type: "int", nullable: false),
                    TermsVersion = table.Column<int>(type: "int", nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalAcceptances", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_LegalAcceptances_UserId_AcceptedAtUtc", schema: "platform", table: "LegalAcceptances", columns: new[] { "UserId", "AcceptedAtUtc" });
        }
    }
}
