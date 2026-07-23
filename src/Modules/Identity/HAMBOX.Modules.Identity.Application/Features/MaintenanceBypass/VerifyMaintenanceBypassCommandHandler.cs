using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.MaintenanceBypass;

internal sealed class VerifyMaintenanceBypassCommandHandler(
    IPlatformSettingsProvider platformSettings,
    IMaintenanceBypassTokenIssuer tokenIssuer)
    : IRequestHandler<VerifyMaintenanceBypassCommand, Result<MaintenanceBypassResponse>>
{
    private static readonly TimeSpan TokenValidity = TimeSpan.FromDays(30);

    public async Task<Result<MaintenanceBypassResponse>> Handle(
        VerifyMaintenanceBypassCommand request,
        CancellationToken cancellationToken)
    {
        if (!await platformSettings.VerifyMaintenanceBypassPasswordAsync(request.Password, cancellationToken))
        {
            return Result.Failure<MaintenanceBypassResponse>(IdentityErrors.InvalidCredentials);
        }

        var (token, expiresOnUtc) = tokenIssuer.Issue(TokenValidity);
        return Result.Success(new MaintenanceBypassResponse(token, expiresOnUtc));
    }
}
