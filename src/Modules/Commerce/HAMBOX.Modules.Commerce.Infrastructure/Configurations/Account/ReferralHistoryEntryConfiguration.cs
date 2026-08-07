using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

/// <summary>
/// Configures the <see cref="ReferralHistoryEntry"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ReferralHistoryEntryConfiguration : IEntityTypeConfiguration<ReferralHistoryEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReferralHistoryEntry> builder)
    {
        builder.ToTable("ReferralHistoryEntries");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReferrerUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.ReferredUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.ReferredEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.PointsEarned)
            .IsRequired();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.ExpiresOnUtc);

        builder.Property(r => r.QualifiedOnUtc);

        builder.Property(r => r.RewardedOnUtc);

        builder.Property(r => r.CreatedOnUtc)
            .IsRequired();

        builder.Property(r => r.ModifiedOnUtc);

        builder.HasIndex(r => r.ReferrerUserId)
            .HasDatabaseName("IX_ReferralHistoryEntries_ReferrerUserId");

        builder.HasIndex(r => r.ReferredUserId)
            .IsUnique()
            .HasDatabaseName("IX_ReferralHistoryEntries_ReferredUserId");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("IX_ReferralHistoryEntries_Status");

        // Concurrency: protects the state-machine transitions (Pending -> Qualified -> Rewarded,
        // Pending -> Expired, Qualified/Rewarded -> Reversed) against two concurrent order
        // completions/refunds racing on the same entry. A real, app-managed Guid rather than
        // ProductConfiguration's SQL Server rowversion — portable across every EF Core provider
        // (including SQLite in tests), since the entity itself bumps the value on each transition
        // instead of relying on a database-generated column.
        builder.Property(r => r.ConcurrencyStamp)
            .IsConcurrencyToken();
    }
}
