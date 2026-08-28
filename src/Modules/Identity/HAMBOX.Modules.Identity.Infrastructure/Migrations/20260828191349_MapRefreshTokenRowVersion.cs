using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MapRefreshTokenRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: the RowVersion column was already physically added by the earlier
            // AddRefreshTokenRowVersionConcurrency migration (20260823181553). That migration's
            // column was never mapped into the EF model until RefreshTokenConfiguration was updated
            // alongside this migration, so EF's model-diff sees it as "new" — this migration exists
            // only to bring the model snapshot in sync, not to touch the schema a second time (which
            // would fail with "column already exists" on any database that already ran the earlier
            // migration, i.e. every database this app has ever migrated).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // See Up() — deliberately a no-op. Reverting the mapping is DropColumn's job in the
            // earlier migration, not this one.
        }
    }
}
