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
    }
}
