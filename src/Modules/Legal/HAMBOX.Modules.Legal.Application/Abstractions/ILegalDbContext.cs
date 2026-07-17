using HAMBOX.Modules.Legal.Domain.Legal;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Legal.Application.Abstractions;

public interface ILegalDbContext
{
    DbSet<LegalSection> LegalSections { get; }
    DbSet<LegalSectionVersion> LegalSectionVersions { get; }
    DbSet<LegalSectionAuditLog> LegalSectionAuditLogs { get; }
    DbSet<LegalSectionAcceptance> LegalSectionAcceptances { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
