using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Messaging.Infrastructure.Configurations;

internal sealed class WhatsAppBotConfigAuditLogConfiguration : IEntityTypeConfiguration<WhatsAppBotConfigAuditLog>
{
    public void Configure(EntityTypeBuilder<WhatsAppBotConfigAuditLog> builder)
    {
        builder.ToTable("WhatsAppBotConfigAuditLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(l => l.Target).HasMaxLength(64);
        builder.Property(l => l.OldValue).HasMaxLength(1000);
        builder.Property(l => l.NewValue).HasMaxLength(1000);
        builder.Property(l => l.ActorUserId).HasMaxLength(64);
        builder.HasIndex(l => l.CreatedOnUtc);
    }
}
