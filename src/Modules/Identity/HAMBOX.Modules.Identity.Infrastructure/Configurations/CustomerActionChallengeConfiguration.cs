using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class CustomerActionChallengeConfiguration : IEntityTypeConfiguration<CustomerActionChallenge>
{
    public void Configure(EntityTypeBuilder<CustomerActionChallenge> builder)
    {
        builder.ToTable("CustomerActionChallenges");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.Purpose).IsRequired().HasMaxLength(64);
        builder.Property(c => c.ScopeKey).IsRequired().HasMaxLength(128);
        builder.Property(c => c.CodeHash).IsRequired().HasMaxLength(128);
        builder.Property(c => c.ExpiresOnUtc).IsRequired();
        builder.Property(c => c.LastSentOnUtc).IsRequired();
        builder.Property(c => c.AttemptCount).IsRequired();
        builder.Property(c => c.LockedUntilUtc);
        builder.Property(c => c.UsedOnUtc);
        builder.Property(c => c.CreatedOnUtc).IsRequired();
        builder.Property(c => c.ModifiedOnUtc);

        builder.Ignore(c => c.IsUsed);
        builder.Ignore(c => c.IsExpired);
        builder.Ignore(c => c.IsLocked);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.UserId, c.Purpose, c.ScopeKey })
            .HasDatabaseName("IX_CustomerActionChallenges_UserId_Purpose_ScopeKey");
    }
}
