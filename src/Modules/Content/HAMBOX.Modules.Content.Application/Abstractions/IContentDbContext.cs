using HAMBOX.Modules.Content.Domain.LandingPages;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Abstractions;

public interface IContentDbContext
{
    DbSet<LandingPageTemplate> LandingPageTemplates { get; }
    DbSet<LandingPageAuditLog> LandingPageAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
