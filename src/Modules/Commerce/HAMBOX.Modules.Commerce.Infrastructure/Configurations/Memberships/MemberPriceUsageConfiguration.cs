using HAMBOX.Modules.Commerce.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Memberships;

internal sealed class MemberPriceUsageConfiguration : IEntityTypeConfiguration<MemberPriceUsage>
{
    public void Configure(EntityTypeBuilder<MemberPriceUsage> builder)
    {
        builder.ToTable("MemberPriceUsages");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.UserId).IsRequired().HasMaxLength(450);
        builder.Property(u => u.PeriodKey).IsRequired().HasMaxLength(10);
        builder.HasIndex(u => new { u.OrderId, u.ProductVariantId }).IsUnique().HasDatabaseName("UX_MemberPriceUsages_Order_Variant");
        builder.HasIndex(u => new { u.UserId, u.PeriodKey }).HasDatabaseName("IX_MemberPriceUsages_User_Period");
    }
}
