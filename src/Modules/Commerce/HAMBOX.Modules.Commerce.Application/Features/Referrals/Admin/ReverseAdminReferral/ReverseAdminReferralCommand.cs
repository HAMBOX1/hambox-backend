using HAMBOX.Modules.Commerce.Application.Contracts.Referrals;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.ReverseAdminReferral;

public sealed record ReverseAdminReferralCommand(Guid ReferralId) : IRequest<Result<AdminReferralDetailDto>>;
