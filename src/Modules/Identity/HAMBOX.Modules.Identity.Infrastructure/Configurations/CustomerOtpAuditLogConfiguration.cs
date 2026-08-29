using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class CustomerOtpAuditLogConfiguration : IEntityTypeConfiguration<CustomerOtpAuditLog>
{
    public void Configure(EntityTypeBuilder<CustomerOtpAuditLog> builder)
    {
        builder.ToTable("CustomerOtpAuditLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.UserId);
        builder.Property(l => l.TokenId);
        builder.Property(l => l.Purpose).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(l => l.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(l => l.IssuedOnUtc);
        builder.Property(l => l.ExpiresOnUtc);
        builder.Property(l => l.UsedOnUtc);
        builder.Property(l => l.IpAddress).HasMaxLength(45);
        builder.Property(l => l.UserAgent).HasMaxLength(512);
        builder.Property(l => l.CorrelationId).HasMaxLength(64);
        builder.Property(l => l.EmailDeliveryStatus).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(l => l.Description).HasMaxLength(512);
        builder.Property(l => l.OccurredOnUtc).IsRequired();
        builder.Property(l => l.CreatedOnUtc).IsRequired();
        builder.Property(l => l.ModifiedOnUtc);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => l.UserId).HasDatabaseName("IX_CustomerOtpAuditLogs_UserId");
        builder.HasIndex(l => l.TokenId).HasDatabaseName("IX_CustomerOtpAuditLogs_TokenId");
        builder.HasIndex(l => l.OccurredOnUtc).HasDatabaseName("IX_CustomerOtpAuditLogs_OccurredOnUtc");
        builder.HasIndex(l => l.Purpose).HasDatabaseName("IX_CustomerOtpAuditLogs_Purpose");
    }
}
