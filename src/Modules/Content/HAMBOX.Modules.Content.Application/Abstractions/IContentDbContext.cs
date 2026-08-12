using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.Modules.Content.Domain.LandingPages;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Abstractions;

public interface IContentDbContext
{
    DbSet<LandingPageTemplate> LandingPageTemplates { get; }
    DbSet<LandingPageAuditLog> LandingPageAuditLogs { get; }
    DbSet<Faq> Faqs { get; }
    DbSet<FaqCategory> FaqCategories { get; }
    DbSet<FaqAuditLog> FaqAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
