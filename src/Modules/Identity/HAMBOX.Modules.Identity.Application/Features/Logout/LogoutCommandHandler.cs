using HAMBOX.Modules.Identity.Application.Abstractions;
using DomainRefreshToken = HAMBOX.Modules.Identity.Domain.Tokens.RefreshToken;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Logout;

/// <summary>
/// Handler for the <see cref="LogoutCommand"/> command.
/// </summary>
internal sealed class LogoutCommandHandler(IIdentityDbContext dbContext) : IRequestHandler<LogoutCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = DomainRefreshToken.GetLookupHash(request.RefreshToken);
        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == tokenHash, cancellationToken);

        if (token is null)
        {
            return Result.Success();
        }

        if (!token.IsRevoked)
        {
            token.Revoke();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
