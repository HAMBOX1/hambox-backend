using HAMBOX.Modules.Commerce.Application.Contracts.Referrals;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.GetAdminReferralById;

public sealed record GetAdminReferralByIdQuery(Guid ReferralId) : IRequest<Result<AdminReferralDetailDto>>;
