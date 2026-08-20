using HAMBOX.Modules.Messaging.Domain.BotConfiguration;
using HAMBOX.Modules.Messaging.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Messaging.Application.Abstractions;

public interface IMessagingDbContext
{
    DbSet<WhatsAppConversationSession> WhatsAppConversationSessions { get; }

    DbSet<WhatsAppBotConfiguration> WhatsAppBotConfigurations { get; }

    DbSet<WhatsAppMenuItem> WhatsAppMenuItems { get; }

    DbSet<WhatsAppBotConfigAuditLog> WhatsAppBotConfigAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
