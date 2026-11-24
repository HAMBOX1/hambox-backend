using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

internal sealed class ReferralLegacyCodeConfiguration : IEntityTypeConfiguration<ReferralLegacyCode>
{
    public void Configure(EntityTypeBuilder<ReferralLegacyCode> builder)
    {
        builder.ToTable("ReferralLegacyCodes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(64);
        builder.Property(c => c.OwnerUserId).IsRequired().HasMaxLength(450);
        builder.Property(c => c.Source).IsRequired().HasMaxLength(30);
        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("UX_ReferralLegacyCodes_Code");
    }
}
