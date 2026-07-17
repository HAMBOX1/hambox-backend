using HAMBOX.Modules.Identity.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class CountryRestrictionConfiguration : IEntityTypeConfiguration<CountryRestriction>
{
    public void Configure(EntityTypeBuilder<CountryRestriction> builder)
    {
        builder.ToTable("CountryRestrictions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(c => c.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.Reason).IsRequired().HasMaxLength(500);
        builder.Property(c => c.Notes).HasMaxLength(2000);
        builder.Property(c => c.ExpiresOnUtc);
        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.ModifiedBy).HasMaxLength(256);
        builder.Property(c => c.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(c => c.DeletedOnUtc);
        builder.Property(c => c.CreatedOnUtc).IsRequired();
        builder.Property(c => c.ModifiedOnUtc);

        builder.Ignore(c => c.IsPermanent);
        builder.Ignore(c => c.IsCurrentlyActive);

        builder.HasIndex(c => c.CountryCode)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_CountryRestrictions_CountryCode");
    }
}
