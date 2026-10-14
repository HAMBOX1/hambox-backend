using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.GetPaymentWebhookLogs;

public sealed record PaymentWebhookLogDto(
    Guid Id,
    string Provider,
    string SignatureVerificationResult,
    string ProcessingResult,
    string? GatewayReference,
    string? MerchantReference,
    string RawPayload,
    string? SourceIp,
    Guid? OrderId,
    DateTimeOffset ReceivedOnUtc);

public sealed record GetPaymentWebhookLogsQuery(string? Provider, string? ProcessingResult, int Take = 100)
    : IRequest<Result<IReadOnlyList<PaymentWebhookLogDto>>>;

internal sealed class GetPaymentWebhookLogsQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetPaymentWebhookLogsQuery, Result<IReadOnlyList<PaymentWebhookLogDto>>>
{
    public async Task<Result<IReadOnlyList<PaymentWebhookLogDto>>> Handle(
        GetPaymentWebhookLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.PaymentWebhookLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            query = query.Where(l => l.Provider == request.Provider);
        }

        if (!string.IsNullOrWhiteSpace(request.ProcessingResult))
        {
            query = query.Where(l => l.ProcessingResult == request.ProcessingResult);
        }

        var take = Math.Clamp(request.Take, 1, 500);
        var rows = await query
            .OrderByDescending(l => l.ReceivedOnUtc)
            .Take(take)
            .Select(l => new PaymentWebhookLogDto(
                l.Id, l.Provider, l.SignatureVerificationResult, l.ProcessingResult, l.GatewayReference,
                l.MerchantReference, l.RawPayload, l.SourceIp, l.OrderId, l.ReceivedOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PaymentWebhookLogDto>>(rows);
    }
}
