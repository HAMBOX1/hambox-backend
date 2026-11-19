using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Customers;

/// <summary>One customer row of the admin Customers screen: the account plus the store's view of their activity.</summary>
public sealed record CustomerListItemDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Status,
    bool EmailConfirmed,
    string PreferredLanguage,
    string PreferredCurrency,
    DateTimeOffset RegisteredOnUtc,
    int OrderCount,
    int PaidOrderCount,
    decimal PaidSpendUsd,
    DateTimeOffset? LastOrderOnUtc,
    string? MembershipPlan,
    string? MembershipStatus,
    DateTime? MembershipExpiresOnUtc,
    int PointsBalance,
    string? ReferralCode,
    string? ReferralTier);

/// <param name="Member">null = everyone, true = only customers with an active membership, false = only those without.</param>
public sealed record GetCustomersQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm,
    string? Status,
    bool? Member,
    DateTimeOffset? RegisteredFromUtc,
    DateTimeOffset? RegisteredToUtc) : IRequest<Result<PagedResult<CustomerListItemDto>>>;

internal sealed class GetCustomersQueryHandler(IIdentityDbContext db, ICustomerActivityReader activity)
    : IRequestHandler<GetCustomersQuery, Result<PagedResult<CustomerListItemDto>>>
{
    public async Task<Result<PagedResult<CustomerListItemDto>>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<UserStatus>(request.Status, true, out var status))
        {
            query = query.Where(u => u.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(u =>
                u.Email.Contains(term) ||
                u.FirstName.Contains(term) ||
                u.LastName.Contains(term) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }

        if (request.RegisteredFromUtc is { } from)
        {
            query = query.Where(u => u.CreatedOnUtc >= from);
        }

        if (request.RegisteredToUtc is { } to)
        {
            query = query.Where(u => u.CreatedOnUtc <= to);
        }

        if (request.Member is { } wantsMember)
        {
            var memberIds = (await activity.GetActiveMemberUserIdsAsync(cancellationToken))
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(guid => guid != Guid.Empty)
                .ToList();
            query = wantsMember ? query.Where(u => memberIds.Contains(u.Id)) : query.Where(u => !memberIds.Contains(u.Id));
        }

        var total = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderByDescending(u => u.CreatedOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new
            {
                u.Id, u.Email, u.FirstName, u.LastName, u.PhoneNumber, u.Status, u.EmailConfirmed,
                u.PreferredLanguage, u.PreferredCurrency, u.CreatedOnUtc,
            })
            .ToListAsync(cancellationToken);

        var activityById = await activity.GetActivityAsync(page.Select(u => u.Id.ToString()).ToList(), cancellationToken);

        var items = page.Select(u =>
        {
            activityById.TryGetValue(u.Id.ToString(), out var a);
            return new CustomerListItemDto(
                u.Id, u.Email, u.FirstName, u.LastName, u.PhoneNumber, u.Status.ToString(), u.EmailConfirmed,
                u.PreferredLanguage, u.PreferredCurrency, u.CreatedOnUtc,
                a?.OrderCount ?? 0, a?.PaidOrderCount ?? 0, a?.PaidSpendUsd ?? 0m, a?.LastOrderOnUtc,
                a?.MembershipPlan, a?.MembershipStatus, a?.MembershipExpiresOnUtc,
                a?.PointsBalance ?? 0, a?.ReferralCode, a?.ReferralTier);
        }).ToList();

        return Result.Success(new PagedResult<CustomerListItemDto>(items, request.PageNumber, request.PageSize, total));
    }
}
