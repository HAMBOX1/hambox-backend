using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Themes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeHardeningPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasEverBeenPublished",
                schema: "platform",
                table: "ThemeVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "platform",
                table: "ThemeVersions",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                schema: "platform",
                table: "ThemeSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "platform",
                table: "StoreThemes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            // Backfill (primary): any version that is currently the live published one must keep
            // its immutability guarantee under the new HasEverBeenPublished flag, otherwise this
            // migration would momentarily regress the exact protection it's adding.
            migrationBuilder.Sql(
                "UPDATE [platform].[ThemeVersions] SET [HasEverBeenPublished] = 1 WHERE [IsPublished] = 1;");

            // Backfill (enrichment): recover superseded versions that were published — or
            // restored via rollback, which republishes a version the same way — at some point in
            // the past but are no longer the current live version, so the primary backfill above
            // would miss them. ThemeAuditLogs has recorded every publish/rollback event
            // atomically alongside the state change (same handler, same SaveChangesAsync call)
            // since this module's very first migration. No code path in this application has
            // ever set IsPublished outside PublishThemeCommandHandler, RollbackThemeCommandHandler,
            // or the one-time theme seeder (which publishes each seed theme's version exactly
            // once and never supersedes it) — so an exact versionId match against
            // Action IN (2, 9) (Published, RolledBack) is a reliable historical record of this
            // application's own publish history.
            //
            // Safety: this UPDATE only ever sets the flag to 1 (never back to 0), is scoped to
            // rows still at 0, and is idempotent — running it again after it has already run
            // finds nothing left to update.
            //
            // LIMITATION — deliberately NOT resolved by this migration: a superseded version with
            // no matching ThemeAuditLogs row for its exact Id (audit rows were ever deleted, or
            // IsPublished was ever changed by something outside this application's own code — a
            // manual UPDATE, a restore from an earlier backup, etc.) is not recoverable from data
            // this application holds, and is intentionally left at HasEverBeenPublished = 0
            // rather than guessed. Such a version would remain editable under UpdateTokens's new
            // HasEverBeenPublished guard, i.e. the immutability loophole this migration closes for
            // provable history stays open for that specific row. This is a known, accepted gap —
            // confirm against the actual database's operational history (has anyone ever run a
            // manual UPDATE against ThemeVersions/ThemeAuditLogs, or restored from a backup that
            // predates some audit rows?) before treating the immutability guarantee as airtight
            // for versions that predate this migration.
            migrationBuilder.Sql(@"
                UPDATE tv
                SET tv.[HasEverBeenPublished] = 1
                FROM [platform].[ThemeVersions] tv
                WHERE tv.[HasEverBeenPublished] = 0
                  AND EXISTS (
                    SELECT 1
                    FROM [platform].[ThemeAuditLogs] al
                    WHERE al.[ThemeId] = tv.[ThemeId]
                      AND al.[Action] IN (2, 9) -- Published, RolledBack
                      AND TRY_CAST(JSON_VALUE(al.[DetailsJson], '$.versionId') AS UNIQUEIDENTIFIER) = tv.[Id]
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasEverBeenPublished",
                schema: "platform",
                table: "ThemeVersions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "platform",
                table: "ThemeVersions");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "platform",
                table: "ThemeSchedules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "platform",
                table: "StoreThemes");
        }
    }
}
