using HAMBOX.Modules.Identity.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Identity.Infrastructure.Configurations;

internal sealed class AuthorizationAuditLogConfiguration : IEntityTypeConfiguration<AuthorizationAuditLog>
{
    public void Configure(EntityTypeBuilder<AuthorizationAuditLog> builder)
    {
        builder.ToTable("AuthorizationAuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityId);
        builder.Property(a => a.ActorUserId).IsRequired();
        builder.Property(a => a.Details).HasMaxLength(4000);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.CreatedOnUtc).IsRequired();
        builder.Property(a => a.ModifiedOnUtc);

        builder.HasIndex(a => a.CreatedOnUtc).HasDatabaseName("IX_AuthorizationAuditLogs_CreatedOnUtc");
        builder.HasIndex(a => a.ActorUserId).HasDatabaseName("IX_AuthorizationAuditLogs_ActorUserId");
    }
}
