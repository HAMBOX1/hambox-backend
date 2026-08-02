using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.LoginHistory;

/// <summary>
/// Lists login attempts (successful and failed) across all users for admin investigation.
/// Pass <paramref name="UserId"/> to scope to a single user's history (e.g. from a device or
/// user detail drawer).
/// </summary>
public sealed record GetLoginHistoryQuery(
    int PageNumber,
    int PageSize,
    Guid? UserId = null,
    bool? IsSuccessful = null,
    string? CountryCode = null,
    string? RiskLevel = null,
    string? Fingerprint = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? SearchTerm = null) : IRequest<Result<PagedResult<LoginHistoryDto>>>;
