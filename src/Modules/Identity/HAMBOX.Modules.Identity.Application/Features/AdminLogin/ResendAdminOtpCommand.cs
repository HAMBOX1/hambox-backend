using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.AdminLogin;

public sealed record ResendAdminOtpCommand(
    Guid ChallengeId,
    string IpAddress) : IRequest<Result<AdminLoginChallengeResponse>>;
