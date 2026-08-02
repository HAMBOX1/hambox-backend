using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Sessions;

internal sealed class GetUserSessionsQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetUserSessionsQuery, Result<IReadOnlyCollection<UserSessionDto>>>
{
    public async Task<Result<IReadOnlyCollection<UserSessionDto>>> Handle(
        GetUserSessionsQuery request,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId)
            .OrderByDescending(s => s.StartedOnUtc)
            .Take(50)
            .Select(s => new UserSessionDto(
                s.Id,
                s.AuthContext,
                s.IpAddress,
                s.UserAgent,
                s.BrowserName,
                s.DeviceName,
                s.StartedOnUtc,
                s.LastActivityOnUtc,
                s.EndedOnUtc,
                s.EndedOnUtc == null))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyCollection<UserSessionDto>>(sessions);
    }
}
