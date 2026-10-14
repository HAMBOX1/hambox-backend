using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class AdminTotpCredentialConfiguration : IEntityTypeConfiguration<AdminTotpCredential>
{
    public void Configure(EntityTypeBuilder<AdminTotpCredential> builder)
    {
        builder.ToTable("AdminTotpCredentials");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.ProtectedSecret).IsRequired().HasMaxLength(1024);
        builder.Property(c => c.IsConfirmed).IsRequired();
        builder.Property(c => c.ConfirmedOnUtc);
        builder.Property(c => c.LastUsedTimeStep).IsRequired();
        builder.Property(c => c.CreatedOnUtc).IsRequired();
        builder.Property(c => c.ModifiedOnUtc);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.UserId).IsUnique().HasDatabaseName("IX_AdminTotpCredentials_UserId");
    }
}

internal sealed class AdminRecoveryCodeConfiguration : IEntityTypeConfiguration<AdminRecoveryCode>
{
    public void Configure(EntityTypeBuilder<AdminRecoveryCode> builder)
    {
        builder.ToTable("AdminRecoveryCodes");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.CodeHash).IsRequired().HasMaxLength(128);
        builder.Property(c => c.UsedOnUtc);
        builder.Property(c => c.CreatedOnUtc).IsRequired();
        builder.Property(c => c.ModifiedOnUtc);

        builder.Ignore(c => c.IsUsed);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.UserId, c.CodeHash }).HasDatabaseName("IX_AdminRecoveryCodes_UserId_CodeHash");
    }
}
