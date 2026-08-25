using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Referrals;
using HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.GetAdminReferralById;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Referrals.Admin.ReverseAdminReferral;

internal sealed class ReverseAdminReferralCommandHandler
    : IRequestHandler<ReverseAdminReferralCommand, Result<AdminReferralDetailDto>>
{
    private readonly ReferralLifecycleService _referralLifecycle;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISender _sender;

    public ReverseAdminReferralCommandHandler(
        ReferralLifecycleService referralLifecycle,
        ICurrentUserService currentUserService,
        ISender sender)
    {
        _referralLifecycle = referralLifecycle;
        _currentUserService = currentUserService;
        _sender = sender;
    }

    public async Task<Result<AdminReferralDetailDto>> Handle(
        ReverseAdminReferralCommand command,
        CancellationToken cancellationToken)
    {
        var actorId = _currentUserService.UserId ?? "system";

        var result = await _referralLifecycle.AdminReverseAsync(command.ReferralId, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<AdminReferralDetailDto>(result.Error);
        }

        return await _sender.Send(new GetAdminReferralByIdQuery(command.ReferralId), cancellationToken);
    }
}
