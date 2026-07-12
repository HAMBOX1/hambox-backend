using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.AdminLogin;

public sealed record AdminLoginCommand(
    string Email,
    string Password,
    string IpAddress,
    string UserAgent) : IRequest<Result<AdminLoginChallengeResponse>>;
