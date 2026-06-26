using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="EmailVerificationToken"/> entity for Entity Framework Core.
/// </summary>
internal sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("EmailVerificationTokens");

        // Primary key
        builder.HasKey(t => t.Id);

        // Properties
        builder.Property(t => t.UserId)
            .IsRequired();

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(t => t.ExpiresOnUtc)
            .IsRequired();

        builder.Property(t => t.UsedOnUtc);

        // Base entity properties
        builder.Property(t => t.CreatedOnUtc)
            .IsRequired();

        builder.Property(t => t.ModifiedOnUtc);

        // Computed properties are not mapped
        builder.Ignore(t => t.IsExpired);
        builder.Ignore(t => t.IsUsed);

        // Relationships
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("IX_EmailVerificationTokens_Token");

        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_EmailVerificationTokens_UserId");
    }
}
