using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Referrals;
using HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.GetAdminReferralById;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.ApproveAdminReferral;

/// <summary>Clears a referral that was held for review as possible farming.</summary>
public sealed record ApproveAdminReferralCommand(Guid ReferralId) : IRequest<Result<AdminReferralDetailDto>>;

internal sealed class ApproveAdminReferralCommandHandler(
    ReferralLifecycleService referralLifecycle,
    ICurrentUserService currentUser,
    ISender sender) : IRequestHandler<ApproveAdminReferralCommand, Result<AdminReferralDetailDto>>
{
    public async Task<Result<AdminReferralDetailDto>> Handle(
        ApproveAdminReferralCommand command, CancellationToken cancellationToken)
    {
        var result = await referralLifecycle.AdminApproveReviewAsync(
            command.ReferralId, currentUser.UserId ?? "system", cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<AdminReferralDetailDto>(result.Error);
        }

        return await sender.Send(new GetAdminReferralByIdQuery(command.ReferralId), cancellationToken);
    }
}
