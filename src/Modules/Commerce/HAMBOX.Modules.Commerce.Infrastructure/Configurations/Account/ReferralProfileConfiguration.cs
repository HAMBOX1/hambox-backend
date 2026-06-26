using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

/// <summary>
/// Configures the <see cref="ReferralProfile"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ReferralProfileConfiguration : IEntityTypeConfiguration<ReferralProfile>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReferralProfile> builder)
    {
        builder.ToTable("ReferralProfiles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.ReferralCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.Tier)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.LifetimePoints)
            .IsRequired();

        builder.Property(r => r.SuccessfulReferrals)
            .IsRequired();

        builder.Property(r => r.CreatedOnUtc)
            .IsRequired();

        builder.Property(r => r.ModifiedOnUtc);

        builder.HasIndex(r => r.UserId)
            .IsUnique()
            .HasDatabaseName("IX_ReferralProfiles_UserId");

        builder.HasIndex(r => r.ReferralCode)
            .IsUnique()
            .HasDatabaseName("IX_ReferralProfiles_ReferralCode");

        builder.Ignore(r => r.DomainEvents);
    }
}
