using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Suppliers.Application.Features.Suppliers;

public sealed record SupplierApiCallLogDto(
    Guid Id,
    string ProviderKey,
    string Method,
    string Url,
    string? RequestBody,
    int? StatusCode,
    string? ResponseBody,
    string? ResponseSummary,
    bool Succeeded,
    string? ErrorMessage,
    int DurationMs,
    Guid? FulfillmentId,
    Guid? HamboxReferenceId,
    Guid? SupplierId,
    Guid? OrderId,
    int? Attempt,
    DateTimeOffset CreatedOnUtc);

public sealed record GetSupplierApiCallLogsQuery(
    Guid? SupplierId, Guid? OrderId, Guid? FulfillmentId, bool? FailedOnly, int Take = 100)
    : IRequest<Result<IReadOnlyList<SupplierApiCallLogDto>>>;

internal sealed class GetSupplierApiCallLogsQueryHandler(ISuppliersDbContext db)
    : IRequestHandler<GetSupplierApiCallLogsQuery, Result<IReadOnlyList<SupplierApiCallLogDto>>>
{
    public async Task<Result<IReadOnlyList<SupplierApiCallLogDto>>> Handle(
        GetSupplierApiCallLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.SupplierApiCallLogs.AsNoTracking().AsQueryable();

        if (request.SupplierId is Guid supplierId)
        {
            query = query.Where(l => l.SupplierId == supplierId);
        }

        if (request.OrderId is Guid orderId)
        {
            query = query.Where(l => l.OrderId == orderId);
        }

        if (request.FulfillmentId is Guid fulfillmentId)
        {
            query = query.Where(l => l.FulfillmentId == fulfillmentId);
        }

        if (request.FailedOnly == true)
        {
            query = query.Where(l => !l.Succeeded);
        }

        var rows = await query
            .OrderByDescending(l => l.CreatedOnUtc)
            .Take(Math.Clamp(request.Take, 1, 500))
            .Select(l => new SupplierApiCallLogDto(
                l.Id, l.ProviderKey, l.Method, l.Url, l.RequestBody, l.StatusCode, l.ResponseBody, l.ResponseSummary,
                l.Succeeded, l.ErrorMessage, l.DurationMs, l.FulfillmentId, l.HamboxReferenceId, l.SupplierId,
                l.OrderId, l.Attempt, l.CreatedOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SupplierApiCallLogDto>>(rows);
    }
}
