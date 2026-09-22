using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

internal sealed class GetDotPaymentStatusQueryHandler(
    ICommerceDbContext commerceDbContext,
    ICurrentUserService currentUserService,
    DotPaymentVerificationService? verificationService = null)
    : IRequestHandler<GetDotPaymentStatusQuery, Result<DotPaymentStatusDto>>
{
    public async Task<Result<DotPaymentStatusDto>> Handle(GetDotPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure<DotPaymentStatusDto>(CommerceErrors.AuthenticationRequired);
        }

        var attempt = await commerceDbContext.PaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PaymentAttemptId, cancellationToken);

        if (attempt is null)
        {
            return Result.Failure<DotPaymentStatusDto>(CommerceErrors.DotPaymentAttemptNotFound);
        }

        // Ownership check folded into the same "not found" error as a genuinely-missing attempt —
        // never reveal that a differently-owned payment attempt exists.
        var order = await commerceDbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == attempt.OrderId && o.UserId == currentUserService.UserId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<DotPaymentStatusDto>(CommerceErrors.DotPaymentAttemptNotFound);
        }

        // Active poll check: if the attempt is pending with a known provider transaction ID
        // (the customer has returned through the redirect and is polling this page), actively
        // re-verify with DOT so that wallet confirmation (PIN entered / app approval) resolves
        // immediately even if the server-to-server webhook has not arrived yet.
        if (attempt.Status == PaymentAttemptStatus.Pending
            && !string.IsNullOrWhiteSpace(attempt.ProviderTransactionId)
            && verificationService is not null)
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

        return Result.Success(new DotPaymentStatusDto(attempt.Id, order.Id, status, completedOrderId));
    }
}
