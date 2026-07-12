using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.AdminLogin;

public sealed record VerifyAdminOtpCommand(
    Guid ChallengeId,
    string Code,
    string IpAddress,
    string UserAgent) : IRequest<Result<AuthTokenResponse>>;
