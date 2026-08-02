using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="LoginHistory"/> entity for Entity Framework Core.
/// </summary>
internal sealed class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        builder.ToTable("LoginHistory");

        // Primary key
        builder.HasKey(h => h.Id);

        // Properties
        builder.Property(h => h.UserId)
            .IsRequired();

        builder.Property(h => h.IpAddress)
            .IsRequired()
            .HasMaxLength(45);

        builder.Property(h => h.UserAgent)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(h => h.IsSuccessful)
            .IsRequired();

        builder.Property(h => h.FailureReason)
            .HasMaxLength(500);

        builder.Property(h => h.CountryCode).HasMaxLength(2);
        builder.Property(h => h.City).HasMaxLength(128);
        builder.Property(h => h.BrowserName).HasMaxLength(128);
        builder.Property(h => h.OsName).HasMaxLength(128);
        builder.Property(h => h.DeviceType).HasMaxLength(32);
        builder.Property(h => h.Fingerprint).HasMaxLength(64);
        builder.Property(h => h.RiskLevel).HasConversion<string>().HasMaxLength(20);

        // Base entity properties
        builder.Property(h => h.CreatedOnUtc)
            .IsRequired();

        builder.Property(h => h.ModifiedOnUtc);

        // Relationships
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(h => h.UserId)
            .HasDatabaseName("IX_LoginHistory_UserId");

        builder.HasIndex(h => new { h.UserId, h.CreatedOnUtc })
            .HasDatabaseName("IX_LoginHistory_UserId_CreatedOnUtc");

        builder.HasIndex(h => h.IpAddress)
            .HasDatabaseName("IX_LoginHistory_IpAddress");

        builder.HasIndex(h => h.Fingerprint)
            .HasDatabaseName("IX_LoginHistory_Fingerprint");
    }
}
