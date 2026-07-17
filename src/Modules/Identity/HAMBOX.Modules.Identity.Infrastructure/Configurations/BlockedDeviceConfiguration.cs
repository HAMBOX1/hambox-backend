using HAMBOX.Modules.Identity.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// Schema-only for now — no service reads/writes <see cref="BlockedDevice"/> yet (see the entity's docs).
/// </summary>
internal sealed class BlockedDeviceConfiguration : IEntityTypeConfiguration<BlockedDevice>
{
    public void Configure(EntityTypeBuilder<BlockedDevice> builder)
    {
        builder.ToTable("BlockedDevices");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.DeviceFingerprint).IsRequired().HasMaxLength(256);
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

        builder.HasIndex(b => b.DeviceFingerprint).HasDatabaseName("IX_BlockedDevices_DeviceFingerprint");
    }
}
