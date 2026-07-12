using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

internal sealed class SessionValidator(IdentityDbContext dbContext) : ISessionValidator
{
    public async Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return false;
        }

        return await dbContext.UserSessions
            .AnyAsync(s => s.Id == sessionId && s.EndedOnUtc == null, cancellationToken);
    }
}
