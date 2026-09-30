using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.OxaPay;

internal sealed class GetOxaPayPaymentStatusQueryHandler(
    ICommerceDbContext commerceDbContext,
    ICurrentUserService currentUserService,
    OxaPayPaymentVerificationService? verificationService = null)
    : IRequestHandler<GetOxaPayPaymentStatusQuery, Result<OxaPayPaymentStatusDto>>
{
    public async Task<Result<OxaPayPaymentStatusDto>> Handle(GetOxaPayPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure<OxaPayPaymentStatusDto>(CommerceErrors.AuthenticationRequired);
        }

        var attempt = await commerceDbContext.PaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PaymentAttemptId, cancellationToken);

        if (attempt is null)
        {
            return Result.Failure<OxaPayPaymentStatusDto>(CommerceErrors.OxaPayPaymentAttemptNotFound);
        }

        // Ownership check folded into the same "not found" error as a genuinely-missing attempt —
        // never reveal that a differently-owned payment attempt exists.
        var order = await commerceDbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == attempt.OrderId && o.UserId == currentUserService.UserId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OxaPayPaymentStatusDto>(CommerceErrors.OxaPayPaymentAttemptNotFound);
        }

        // Active poll check: the customer landed back on our result page — actively re-verify with
        // OxaPay so payment resolves immediately even if the webhook hasn't arrived yet.
        if (attempt.Status == PaymentAttemptStatus.Pending && verificationService is not null)
        {
            await verificationService.VerifyAndFinalizeAsync(attempt.Id, cancellationToken);

            attempt = await commerceDbContext.PaymentAttempts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.PaymentAttemptId, cancellationToken)
                ?? attempt;
        }

        var status = attempt.Status switch
        {
            PaymentAttemptStatus.Succeeded => "Succeeded",
            PaymentAttemptStatus.Failed => "Failed",
            PaymentAttemptStatus.Expired => "Expired",
            _ => "Pending",
        };

        var completedOrderId = attempt.Status == PaymentAttemptStatus.Succeeded ? order.Id : (Guid?)null;

        return Result.Success(new OxaPayPaymentStatusDto(attempt.Id, order.Id, status, completedOrderId));
    }
}
