using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Login;

/// <summary>
/// Command to login a user and return authentication tokens.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The user's password.</param>
/// <param name="IpAddress">The IP address from which the request originated.</param>
/// <param name="UserAgent">The user agent of the client making the request.</param>
public sealed record LoginCommand(
    string Email,
    string Password,
    string IpAddress,
    string UserAgent) : IRequest<Result<AuthTokenResponse>>;
