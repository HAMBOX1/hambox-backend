using HAMBOX.Modules.Commerce.Domain.Cosmetics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

internal sealed class ProfileCosmeticConfiguration : IEntityTypeConfiguration<ProfileCosmetic>
{
    public void Configure(EntityTypeBuilder<ProfileCosmetic> builder)
    {
        builder.ToTable("ProfileCosmetics");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Kind).HasConversion<int>();
        builder.Property(c => c.Unlock).HasConversion<int>();
        builder.Property(c => c.Key).IsRequired().HasMaxLength(60);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(100);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(100);
        builder.Property(c => c.ImageUrl).HasMaxLength(500);
        builder.Property(c => c.AccentColor).HasMaxLength(20);
        builder.Property(c => c.UnlockRankKey).HasMaxLength(30);
        builder.Ignore(c => c.DomainEvents);
        builder.HasIndex(c => c.Key).IsUnique().HasDatabaseName("UX_ProfileCosmetics_Key");
    }
}

internal sealed class CustomerCosmeticConfiguration : IEntityTypeConfiguration<CustomerCosmetic>
{
    public void Configure(EntityTypeBuilder<CustomerCosmetic> builder)
    {
        builder.ToTable("CustomerCosmetics");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(c => new { c.UserId, c.CosmeticId }).IsUnique().HasDatabaseName("UX_CustomerCosmetics_User_Cosmetic");
    }
}

internal sealed class CustomerProfileStyleConfiguration : IEntityTypeConfiguration<CustomerProfileStyle>
{
    public void Configure(EntityTypeBuilder<CustomerProfileStyle> builder)
    {
        builder.ToTable("CustomerProfileStyles");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(s => s.UserId).IsUnique().HasDatabaseName("UX_CustomerProfileStyles_UserId");
    }
}
