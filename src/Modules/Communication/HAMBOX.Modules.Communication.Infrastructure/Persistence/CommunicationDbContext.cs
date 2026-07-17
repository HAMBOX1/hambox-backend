using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Domain.Communication;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Infrastructure.Persistence;

public sealed class CommunicationDbContext(DbContextOptions<CommunicationDbContext> options)
    : DbContext(options), ICommunicationDbContext
{
    public DbSet<CommunicationMessage> CommunicationMessages => Set<CommunicationMessage>();
    public DbSet<CommunicationTemplate> CommunicationTemplates => Set<CommunicationTemplate>();
    public DbSet<CommunicationTemplateVersion> CommunicationTemplateVersions => Set<CommunicationTemplateVersion>();
    public DbSet<CommunicationProviderConfig> CommunicationProviderConfigs => Set<CommunicationProviderConfig>();
    public DbSet<CommunicationPreference> CommunicationPreferences => Set<CommunicationPreference>();
    public DbSet<CommunicationAuditLog> CommunicationAuditLogs => Set<CommunicationAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("communication");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunicationDbContext).Assembly);
    }
}
