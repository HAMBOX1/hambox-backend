using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Promotions;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.DiscountValue).HasPrecision(18, 2);
        builder.Property(p => p.Type).HasConversion<int>();
        builder.Property(p => p.DiscountType).HasConversion<int>();
        builder.Property(p => p.Status).HasConversion<int>();
        builder.Property(p => p.IsPublished).IsRequired();
        builder.Property(p => p.TotalRedemptions).IsRequired();

        builder.HasMany(p => p.Conditions)
            .WithOne()
            .HasForeignKey(c => c.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Targets)
            .WithOne()
            .HasForeignKey(t => t.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.CouponCodes)
            .WithOne()
            .HasForeignKey(c => c.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Promotion.Conditions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Promotion.Targets))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Promotion.CouponCodes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => new { p.Status, p.IsPublished, p.Type })
            .HasDatabaseName("IX_Promotions_Status_Published_Type");

        builder.Ignore(p => p.DomainEvents);
    }
}
