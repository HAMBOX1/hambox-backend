using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="RefreshToken"/> entity for Entity Framework Core.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

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

        builder.Property(t => t.RevokedOnUtc);

        builder.Property(t => t.AuthContext)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue("customer");

        builder.Property(t => t.SessionId)
            .IsRequired();

        builder.Property(t => t.ReplacedByTokenHash)
            .HasMaxLength(512);

        builder.Property(t => t.IsPersistent)
            .IsRequired()
            .HasDefaultValue(false);

        // Base entity properties
        builder.Property(t => t.CreatedOnUtc)
            .IsRequired();

        builder.Property(t => t.ModifiedOnUtc);

        // Concurrency — see RefreshTokenCommandHandler: guards against two simultaneous refresh
        // requests both rotating the same still-active token (a plain read-then-write race that a
        // unique index on Token alone cannot catch, since both requests are UPDATing the same
        // existing row, not INSERTing a duplicate).
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        // Computed properties are not mapped
        builder.Ignore(t => t.IsExpired);
        builder.Ignore(t => t.IsRevoked);
        builder.Ignore(t => t.IsActive);

        // Relationships
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_Token");

        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");

        builder.HasIndex(t => t.ExpiresOnUtc)
            .HasDatabaseName("IX_RefreshTokens_ExpiresOnUtc");
    }
}
