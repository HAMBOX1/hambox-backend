using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;

internal sealed class GetDotFawryPaymentStatusQueryHandler(
    ICommerceDbContext commerceDbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetDotFawryPaymentStatusQuery, Result<DotFawryPaymentStatusDto>>
{
    public async Task<Result<DotFawryPaymentStatusDto>> Handle(GetDotFawryPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure<DotFawryPaymentStatusDto>(CommerceErrors.AuthenticationRequired);
        }

        var attempt = await commerceDbContext.PaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PaymentAttemptId, cancellationToken);

        if (attempt is null)
        {
            return Result.Failure<DotFawryPaymentStatusDto>(CommerceErrors.DotFawryPaymentAttemptNotFound);
        }

        // Ownership check folded into the same "not found" error as a genuinely-missing attempt —
        // never reveal that a differently-owned payment attempt exists.
        var order = await commerceDbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == attempt.OrderId && o.UserId == currentUserService.UserId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<DotFawryPaymentStatusDto>(CommerceErrors.DotFawryPaymentAttemptNotFound);
        }

        var status = attempt.Status switch
        {
            PaymentAttemptStatus.Succeeded => "Succeeded",
            PaymentAttemptStatus.Failed => "Failed",
            PaymentAttemptStatus.Expired => "Expired",
            _ => "AwaitingPayment",
        };

        var completedOrderId = attempt.Status == PaymentAttemptStatus.Succeeded ? order.Id : (Guid?)null;
        var walletName = DotFawryWalletOperatorExtensions.TryParseOperatorId(attempt.OperatorId, out var wallet)
            ? wallet.ToString()
            : attempt.OperatorId;

        return Result.Success(new DotFawryPaymentStatusDto(
            attempt.Id, order.Id, status, attempt.ProviderReferenceCode, completedOrderId, walletName));
    }
}
