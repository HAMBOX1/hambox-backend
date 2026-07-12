using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class AdminLoginChallengeConfiguration : IEntityTypeConfiguration<AdminLoginChallenge>
{
    public void Configure(EntityTypeBuilder<AdminLoginChallenge> builder)
    {
        builder.ToTable("AdminLoginChallenges");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.CodeHash).IsRequired().HasMaxLength(128);
        builder.Property(c => c.ExpiresOnUtc).IsRequired();
        builder.Property(c => c.UsedOnUtc);
        builder.Property(c => c.AttemptCount).IsRequired();
        builder.Property(c => c.LastResendOnUtc);
        builder.Property(c => c.LockedUntilUtc);
        builder.Property(c => c.IpAddress).IsRequired().HasMaxLength(45);
        builder.Property(c => c.UserAgent).IsRequired().HasMaxLength(512);
        builder.Property(c => c.CreatedOnUtc).IsRequired();
        builder.Property(c => c.ModifiedOnUtc);

        builder.Ignore(c => c.IsUsed);
        builder.Ignore(c => c.IsExpired);
        builder.Ignore(c => c.IsLocked);
        builder.Ignore(c => c.IsActive);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.UserId).HasDatabaseName("IX_AdminLoginChallenges_UserId");
        builder.HasIndex(c => c.ExpiresOnUtc).HasDatabaseName("IX_AdminLoginChallenges_ExpiresOnUtc");
    }
}
