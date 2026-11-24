using HAMBOX.Modules.Commerce.Domain.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

internal sealed class LegacyCustomerRecordConfiguration : IEntityTypeConfiguration<LegacyCustomerRecord>
{
    public void Configure(EntityTypeBuilder<LegacyCustomerRecord> builder)
    {
        builder.ToTable("LegacyCustomerRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.LegacyId).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Email).HasMaxLength(320);
        builder.Property(r => r.Phone).HasMaxLength(50);
        builder.Property(r => r.Name).HasMaxLength(200);
        builder.Property(r => r.Status).HasConversion<int>();
        builder.Property(r => r.LinkedUserId).HasMaxLength(450);
        builder.Property(r => r.LinkedBy).HasMaxLength(450);
        builder.Property(r => r.LinkReason).HasMaxLength(500);
        builder.HasIndex(r => r.LegacyId).IsUnique().HasDatabaseName("UX_LegacyCustomerRecords_LegacyId");
        builder.HasIndex(r => r.Email).HasDatabaseName("IX_LegacyCustomerRecords_Email");
        builder.HasIndex(r => r.LinkedUserId).HasDatabaseName("IX_LegacyCustomerRecords_LinkedUser");
    }
}

internal sealed class LegacyOrderRecordConfiguration : IEntityTypeConfiguration<LegacyOrderRecord>
{
    public void Configure(EntityTypeBuilder<LegacyOrderRecord> builder)
    {
        builder.ToTable("LegacyOrderRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.LegacyOrderId).IsRequired().HasMaxLength(100);
        builder.Property(r => r.LegacyCustomerId).IsRequired().HasMaxLength(100);
        builder.Property(r => r.TotalAmount).HasPrecision(18, 2);
        builder.Property(r => r.Currency).IsRequired().HasMaxLength(10);
        builder.Property(r => r.Status).HasMaxLength(60);
        builder.HasIndex(r => r.LegacyOrderId).IsUnique().HasDatabaseName("UX_LegacyOrderRecords_LegacyOrderId");
        builder.HasIndex(r => r.LegacyCustomerId).HasDatabaseName("IX_LegacyOrderRecords_Customer");
    }
}
