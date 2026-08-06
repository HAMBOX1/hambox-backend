using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.GoogleLogin;

/// <summary>
/// Command to sign in (or auto-register) a customer using a Google ID token.
/// </summary>
/// <param name="IdToken">The Google ID token obtained by the client from Google Sign-In.</param>
/// <param name="IpAddress">The IP address from which the request originated.</param>
/// <param name="UserAgent">The user agent of the client making the request.</param>
/// <param name="CountryCode">The resolved ISO-3166 alpha-2 country code, if any.</param>
/// <param name="City">The resolved city, if a city-level geolocation resolver is configured.</param>
public sealed record GoogleLoginCommand(
    string IdToken,
    string IpAddress,
    string UserAgent,
    string? CountryCode = null,
    string? City = null) : IRequest<Result<AuthTokenResponse>>;
