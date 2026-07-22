using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.MaintenanceBypass;

internal sealed class VerifyMaintenanceBypassCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IAdminAccessResolver adminAccessResolver,
    IMaintenanceBypassTokenIssuer tokenIssuer)
    : IRequestHandler<VerifyMaintenanceBypassCommand, Result<MaintenanceBypassResponse>>
{
    private static readonly TimeSpan TokenValidity = TimeSpan.FromDays(30);

    public async Task<Result<MaintenanceBypassResponse>> Handle(
        VerifyMaintenanceBypassCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            return Result.Failure<MaintenanceBypassResponse>(IdentityErrors.InvalidCredentials);
        }

        if (user.Status != UserStatus.Active)
        {
            return Result.Failure<MaintenanceBypassResponse>(IdentityErrors.InvalidCredentials);
        }

        if (!await adminAccessResolver.HasAdminPortalAccessAsync(user.Id, cancellationToken))
        {
            return Result.Failure<MaintenanceBypassResponse>(IdentityErrors.InvalidCredentials);
        }

        var (token, expiresOnUtc) = tokenIssuer.Issue(TokenValidity);
        return Result.Success(new MaintenanceBypassResponse(token, expiresOnUtc));
    }
}
