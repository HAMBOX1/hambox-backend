using HAMBOX.Modules.Communication.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Application.Abstractions;

public interface ICommunicationDbContext
{
    DbSet<CommunicationMessage> CommunicationMessages { get; }
    DbSet<CommunicationTemplate> CommunicationTemplates { get; }
    DbSet<CommunicationTemplateVersion> CommunicationTemplateVersions { get; }
    DbSet<CommunicationProviderConfig> CommunicationProviderConfigs { get; }
    DbSet<CommunicationPreference> CommunicationPreferences { get; }
    DbSet<CommunicationAuditLog> CommunicationAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
