using HAMBOX.Modules.Commerce.Domain.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Idempotency;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("IdempotencyKeys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Key).HasMaxLength(200).IsRequired();
        builder.Property(k => k.UserId).HasMaxLength(450);
        builder.Property(k => k.Endpoint).HasMaxLength(200).IsRequired();
        builder.Property(k => k.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.State).HasConversion<int>();
        // No explicit HasColumnType: EF Core's own convention already maps an unbounded string to
        // nvarchar(max) on SQL Server — the literal "nvarchar(max)" string was provider-specific and
        // broke schema creation on any other provider (e.g. SQLite in integration tests), for no
        // behavior difference on SQL Server.
        builder.Property(k => k.SerializedResponse);
        builder.Property(k => k.RowVersion).IsRowVersion();

        builder.HasIndex(k => k.Key).IsUnique().HasDatabaseName("IX_IdempotencyKeys_Key");
        builder.HasIndex(k => k.ExpiresOnUtc).HasDatabaseName("IX_IdempotencyKeys_ExpiresOnUtc");
    }
}
