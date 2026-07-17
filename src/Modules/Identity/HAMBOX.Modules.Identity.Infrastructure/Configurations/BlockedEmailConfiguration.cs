using HAMBOX.Modules.Identity.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class BlockedEmailConfiguration : IEntityTypeConfiguration<BlockedEmail>
{
    public void Configure(EntityTypeBuilder<BlockedEmail> builder)
    {
        builder.ToTable("BlockedEmails");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Pattern).IsRequired().HasMaxLength(320);
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
        builder.Ignore(b => b.IsWildcardDomain);

        builder.HasIndex(b => b.Pattern).HasDatabaseName("IX_BlockedEmails_Pattern");
    }
}
