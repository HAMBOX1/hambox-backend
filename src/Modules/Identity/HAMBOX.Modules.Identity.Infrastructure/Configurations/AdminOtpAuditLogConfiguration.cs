using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class AdminOtpAuditLogConfiguration : IEntityTypeConfiguration<AdminOtpAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminOtpAuditLog> builder)
    {
        builder.ToTable("AdminOtpAuditLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.UserId);
        builder.Property(l => l.ChallengeId);
        builder.Property(l => l.Action).IsRequired().HasMaxLength(64);
        builder.Property(l => l.IpAddress).IsRequired().HasMaxLength(45);
        builder.Property(l => l.Details).HasMaxLength(1024);
        builder.Property(l => l.OccurredOnUtc).IsRequired();
        builder.Property(l => l.CreatedOnUtc).IsRequired();
        builder.Property(l => l.ModifiedOnUtc);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => l.UserId).HasDatabaseName("IX_AdminOtpAuditLogs_UserId");
        builder.HasIndex(l => l.ChallengeId).HasDatabaseName("IX_AdminOtpAuditLogs_ChallengeId");
        builder.HasIndex(l => l.OccurredOnUtc).HasDatabaseName("IX_AdminOtpAuditLogs_OccurredOnUtc");
    }
}
