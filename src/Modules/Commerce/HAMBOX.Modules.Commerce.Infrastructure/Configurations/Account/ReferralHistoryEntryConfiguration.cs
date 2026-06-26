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

        builder.Property(r => r.PointsEarned)
            .IsRequired();

        builder.Property(r => r.CreatedOnUtc)
            .IsRequired();

        builder.Property(r => r.ModifiedOnUtc);

        builder.HasIndex(r => r.ReferrerUserId)
            .HasDatabaseName("IX_ReferralHistoryEntries_ReferrerUserId");

        builder.HasIndex(r => r.ReferredUserId)
            .IsUnique()
            .HasDatabaseName("IX_ReferralHistoryEntries_ReferredUserId");
    }
}
