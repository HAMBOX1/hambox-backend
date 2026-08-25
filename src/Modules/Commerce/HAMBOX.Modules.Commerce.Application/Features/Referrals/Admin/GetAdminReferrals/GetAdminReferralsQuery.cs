using HAMBOX.Modules.Commerce.Application.Contracts.Referrals;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.GetAdminReferrals;

public sealed record GetAdminReferralsQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm,
    string? Status) : IRequest<Result<PagedResult<AdminReferralListItemDto>>>;
