using HAMBOX.Modules.Identity.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class BlockedPhoneConfiguration : IEntityTypeConfiguration<BlockedPhone>
{
    public void Configure(EntityTypeBuilder<BlockedPhone> builder)
    {
        builder.ToTable("BlockedPhones");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Pattern).IsRequired().HasMaxLength(32);
        builder.Property(b => b.Reason).IsRequired().HasMaxLength(500);
        builder.Property(b => b.Notes).HasMaxLength(2000);
        builder.Property(b => b.ExpiresOnUtc);
        builder.Property(b => b.CreatedBy).HasMaxLength(256);
        builder.Property(b => b.ModifiedBy).HasMaxLength(256);
        builder.Property(b => b.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(b => b.DeletedOnUtc);
        builder.Property(b => b.CreatedOnUtc).IsRequired();
        builder.Property(b => b.ModifiedOnUtc);

        builder.Ignore(b => b.IsPermanent);
        builder.Ignore(b => b.IsCurrentlyActive);

        builder.HasIndex(b => b.Pattern).HasDatabaseName("IX_BlockedPhones_Pattern");
    }
}
