using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Sessions;

internal sealed class GetSessionsQueryHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<GetSessionsQuery, Result<IReadOnlyCollection<UserSessionDto>>>
{
    public async Task<Result<IReadOnlyCollection<UserSessionDto>>> Handle(
        GetSessionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var userId))
        {
            return Result.Failure<IReadOnlyCollection<UserSessionDto>>(IdentityErrors.AuthenticationRequired);
        }

        var sessions = await dbContext.UserSessions
            .Where(s => s.UserId == userId)
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
