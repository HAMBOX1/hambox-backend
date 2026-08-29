using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.VerifyEmail;

/// <summary>
/// Command to verify a user's email address using a verification token.
/// </summary>
/// <param name="Token">The email verification token value.</param>
/// <param name="IpAddress">The client IP address, for audit-trail purposes.</param>
/// <param name="UserAgent">The client user agent, for audit-trail purposes.</param>
/// <param name="CorrelationId">The request correlation ID, for audit-trail cross-referencing.</param>
public sealed record VerifyEmailCommand(
    string Token,
    string? IpAddress = null,
    string? UserAgent = null,
    string? CorrelationId = null) : IRequest<Result>;
