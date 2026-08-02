using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="TrustedDevice"/> entity for Entity Framework Core.
/// </summary>
internal sealed class TrustedDeviceConfiguration : IEntityTypeConfiguration<TrustedDevice>
{
    public void Configure(EntityTypeBuilder<TrustedDevice> builder)
    {
        builder.ToTable("TrustedDevices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.UserId).IsRequired();
        builder.Property(d => d.Fingerprint).IsRequired().HasMaxLength(64);
        builder.Property(d => d.BrowserName).HasMaxLength(128);
        builder.Property(d => d.OsName).HasMaxLength(128);
        builder.Property(d => d.DeviceType).HasMaxLength(32);
        builder.Property(d => d.FirstSeenUtc).IsRequired();
        builder.Property(d => d.LastSeenUtc).IsRequired();
        builder.Property(d => d.LastIpAddress).IsRequired().HasMaxLength(45);
        builder.Property(d => d.LastCountryCode).HasMaxLength(2);
        builder.Property(d => d.LastCity).HasMaxLength(128);
        builder.Property(d => d.LoginCount).IsRequired();
        builder.Property(d => d.IsTrusted).IsRequired().HasDefaultValue(false);
        builder.Property(d => d.TrustedOnUtc);
        builder.Property(d => d.TrustedByUserId);
        builder.Property(d => d.IsBlocked).IsRequired().HasDefaultValue(false);
        builder.Property(d => d.BlockedOnUtc);
        builder.Property(d => d.BlockedByUserId);
        builder.Property(d => d.BlockReason).HasMaxLength(500);

        builder.Property(d => d.CreatedOnUtc).IsRequired();
        builder.Property(d => d.ModifiedOnUtc);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.UserId, d.Fingerprint })
            .IsUnique()
            .HasDatabaseName("IX_TrustedDevices_UserId_Fingerprint");

        builder.HasIndex(d => d.Fingerprint)
            .HasDatabaseName("IX_TrustedDevices_Fingerprint");
    }
}
