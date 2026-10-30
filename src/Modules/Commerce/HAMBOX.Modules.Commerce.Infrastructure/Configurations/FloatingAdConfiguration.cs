using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

internal sealed class FloatingAdConfiguration : IEntityTypeConfiguration<FloatingAd>
{
    public void Configure(EntityTypeBuilder<FloatingAd> builder)
    {
        builder.ToTable("FloatingAds");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.TitleEn).HasMaxLength(200).IsRequired();
        builder.Property(a => a.TitleAr).HasMaxLength(200);
        builder.Property(a => a.MessageEn).HasMaxLength(1000);
        builder.Property(a => a.MessageAr).HasMaxLength(1000);
        builder.Property(a => a.ButtonLabelEn).HasMaxLength(100);
        builder.Property(a => a.ButtonLabelAr).HasMaxLength(100);
        builder.Property(a => a.LinkUrl).HasMaxLength(500);
        builder.Property(a => a.Audience).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(a => new { a.IsActive, a.SortOrder });
    }
}
