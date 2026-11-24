using HAMBOX.Modules.Commerce.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Memberships;

internal sealed class MembershipCodeConfiguration : IEntityTypeConfiguration<MembershipCode>
{
    public void Configure(EntityTypeBuilder<MembershipCode> builder)
    {
        builder.ToTable("MembershipCodes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Kind).HasConversion<int>();
        builder.Property(c => c.Note).HasMaxLength(500);
        builder.Property(c => c.CreatedByUserId).HasMaxLength(450);
        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("UX_MembershipCodes_Code");
        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class MembershipCodeRedemptionConfiguration : IEntityTypeConfiguration<MembershipCodeRedemption>
{
    public void Configure(EntityTypeBuilder<MembershipCodeRedemption> builder)
    {
        builder.ToTable("MembershipCodeRedemptions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(r => new { r.CodeId, r.UserId }).IsUnique().HasDatabaseName("UX_MembershipCodeRedemptions_Code_User");
    }
}
