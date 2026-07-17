using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.BlockedIps;

/// <summary>
/// Blocks a single IP address or a CIDR range (IPv4 or IPv6).
/// </summary>
public sealed record CreateBlockedIpCommand(
    string CidrOrAddress,
    string Reason,
    string? Notes,
    DateTimeOffset? ExpiresOnUtc,
    string? IpAddress) : IRequest<Result<Guid>>;
