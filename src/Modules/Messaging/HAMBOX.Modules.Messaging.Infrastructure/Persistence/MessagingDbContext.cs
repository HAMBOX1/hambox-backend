using HAMBOX.Modules.Messaging.Application.Abstractions;
using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using HAMBOX.Modules.Messaging.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Messaging.Infrastructure.Persistence;

public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options)
    : DbContext(options), IMessagingDbContext
{
    public DbSet<WhatsAppConversationSession> WhatsAppConversationSessions => Set<WhatsAppConversationSession>();

    public DbSet<WhatsAppBotConfiguration> WhatsAppBotConfigurations => Set<WhatsAppBotConfiguration>();

    public DbSet<WhatsAppMenuItem> WhatsAppMenuItems => Set<WhatsAppMenuItem>();

    public DbSet<WhatsAppBotConfigAuditLog> WhatsAppBotConfigAuditLogs => Set<WhatsAppBotConfigAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("messaging");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessagingDbContext).Assembly);
    }
}
