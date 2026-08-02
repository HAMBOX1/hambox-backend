using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="UserSession"/> entity for Entity Framework Core.
/// </summary>
internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");

        // Primary key
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.UserId)
            .IsRequired();

        builder.Property(s => s.IpAddress)
            .IsRequired()
            .HasMaxLength(45);

        builder.Property(s => s.UserAgent)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(s => s.StartedOnUtc)
            .IsRequired();

        builder.Property(s => s.LastActivityOnUtc)
            .IsRequired();

        builder.Property(s => s.EndedOnUtc);

        builder.Property(s => s.AuthContext)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue("customer");

        builder.Property(s => s.RefreshTokenId);

        builder.Property(s => s.BrowserName)
            .HasMaxLength(128);

        builder.Property(s => s.OsName)
            .HasMaxLength(128);

        builder.Property(s => s.DeviceName)
            .HasMaxLength(128);

        builder.Property(s => s.CountryCode)
            .HasMaxLength(2);

        builder.Property(s => s.City)
            .HasMaxLength(128);

        builder.Property(s => s.Fingerprint)
            .HasMaxLength(64);

        // Base entity properties
        builder.Property(s => s.CreatedOnUtc)
            .IsRequired();

        builder.Property(s => s.ModifiedOnUtc);

        // Computed properties are not mapped
        builder.Ignore(s => s.IsActive);

        // Relationships
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_UserSessions_UserId");

        builder.HasIndex(s => new { s.UserId, s.EndedOnUtc })
            .HasDatabaseName("IX_UserSessions_UserId_EndedOnUtc");
    }
}
