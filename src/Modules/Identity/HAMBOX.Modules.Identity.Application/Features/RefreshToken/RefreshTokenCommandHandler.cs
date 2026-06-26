using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using DomainRefreshToken = HAMBOX.Modules.Identity.Domain.Tokens.RefreshToken;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Application.Features.RefreshToken;

/// <summary>
/// Handler for the <see cref="RefreshTokenCommand"/> command.
/// </summary>
internal sealed class RefreshTokenCommandHandler(
    IIdentityDbContext dbContext,
    IJwtTokenService jwtTokenService,
    ITokenGenerator tokenGenerator,
    IUserClaimsService userClaimsService,
    IOptions<JwtSettings> jwtSettings) : IRequestHandler<RefreshTokenCommand, Result<AuthTokenResponse>>
{
    /// <inheritdoc />
    public async Task<Result<AuthTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = DomainRefreshToken.GetLookupHash(request.RefreshToken);
        var existingToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == tokenHash, cancellationToken);

        if (existingToken is null || !existingToken.IsActive)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidToken);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == existingToken.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.UserNotFound);
        }

        if (user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AccountNotActive);
        }

        existingToken.Revoke();

        var newRefreshTokenValue = tokenGenerator.GenerateSecureToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpirationDays);
        var (newRefreshToken, _) = DomainRefreshToken.Issue(user.Id, newRefreshTokenValue, refreshExpiresAt);

        var claims = await userClaimsService.GetClaimsAsync(user.Id, cancellationToken);
        var (accessToken, expiresAt) = jwtTokenService.GenerateAccessToken(user, claims);

        dbContext.RefreshTokens.Add(newRefreshToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokenResponse(accessToken, newRefreshTokenValue, expiresAt));
    }
}
