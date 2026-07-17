using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="ApplicationUser"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        // Primary key
        builder.HasKey(u => u.Id);

        // Properties
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(u => u.PreferredLanguage)
            .IsRequired()
            .HasMaxLength(5)
            .HasDefaultValue("en");

        builder.Property(u => u.PreferredCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(u => u.EmailConfirmed)
            .IsRequired();

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(u => u.SecurityStamp)
            .IsRequired()
            .HasMaxLength(64)
            .IsConcurrencyToken();

        builder.Property(u => u.AccessFailedCount)
            .IsRequired();

        builder.Property(u => u.LockoutEnd);

        builder.Property(u => u.TwoFactorEnabled)
            .IsRequired();

        builder.Property(u => u.BlockReason)
            .HasMaxLength(500);

        builder.Property(u => u.BlockNotes)
            .HasMaxLength(2000);

        builder.Property(u => u.BlockExpiresOnUtc);

        builder.Property(u => u.BlockedByUserId);

        // Audit properties (IAuditable)
        builder.Property(u => u.CreatedBy)
            .HasMaxLength(256);

        builder.Property(u => u.ModifiedBy)
            .HasMaxLength(256);

        // Soft delete properties (ISoftDeletable)
        builder.Property(u => u.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.DeletedOnUtc);

        // Base entity properties
        builder.Property(u => u.CreatedOnUtc)
            .IsRequired();

        builder.Property(u => u.ModifiedOnUtc);

        // Concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        // Indexes
        builder.HasIndex(u => u.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("IX_Users_NormalizedEmail");

        builder.HasIndex(u => u.Status)
            .HasDatabaseName("IX_Users_Status");

        builder.HasIndex(u => u.IsDeleted)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Users_IsDeleted");

        // Ignore domain events collection
        builder.Ignore(u => u.DomainEvents);
    }
}
