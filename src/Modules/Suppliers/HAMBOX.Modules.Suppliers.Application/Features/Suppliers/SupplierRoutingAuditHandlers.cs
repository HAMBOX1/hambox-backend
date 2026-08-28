using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Suppliers.Application.Features.Suppliers;

/// <summary>
/// Admin-only read of the cheapest-eligible-supplier routing engine's decision history for one order —
/// surfaced by Commerce's admin order-detail query (<c>GetAdminOrderByIdQueryHandler</c>) via MediatR,
/// exactly the same cross-module composition <c>ISupplierFulfillmentService</c> already uses. Never
/// called from any customer-facing surface — see <c>SupplierRoutingAuditLog</c>'s own remarks for why
/// its contents are safe to show an admin but must never reach a customer.
/// </summary>
public sealed record GetSupplierRoutingHistoryForOrderQuery(Guid OrderId) : IRequest<Result<IReadOnlyList<SupplierRoutingAuditLogDto>>>;

internal sealed class GetSupplierRoutingHistoryForOrderQueryHandler(ISuppliersDbContext dbContext)
    : IRequestHandler<GetSupplierRoutingHistoryForOrderQuery, Result<IReadOnlyList<SupplierRoutingAuditLogDto>>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<IReadOnlyList<SupplierRoutingAuditLogDto>>> Handle(
        GetSupplierRoutingHistoryForOrderQuery request, CancellationToken cancellationToken)
    {
        var rows = await dbContext.SupplierRoutingAuditLogs.AsNoTracking()
            .Where(l => l.OrderId == request.OrderId)
            .OrderBy(l => l.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Result.Success<IReadOnlyList<SupplierRoutingAuditLogDto>>([]);
        }

        var supplierIds = rows.Where(r => r.SelectedSupplierId is Guid).Select(r => r.SelectedSupplierId!.Value).Distinct().ToList();
        var supplierNames = supplierIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Suppliers.AsNoTracking()
                .Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var result = rows.Select(row => new SupplierRoutingAuditLogDto(
            row.Id,
            row.OrderId,
            row.OrderItemId,
            row.SelectedSupplierId,
            row.SelectedSupplierId is Guid sid ? supplierNames.GetValueOrDefault(sid) : null,
            row.SelectedSupplierProductMappingId,
            row.SelectedCostInBaseCurrency,
            row.BaseCurrency,
            row.FallbackOccurred,
            DeserializeCandidates(row.CandidatesJson),
            row.CreatedOnUtc))
            .ToList();

        return Result.Success<IReadOnlyList<SupplierRoutingAuditLogDto>>(result);
    }

    /// <summary>Defensive: a malformed row (should never happen — this module is the only writer) shows as an empty candidate list rather than failing the whole order-detail page.</summary>
    private static IReadOnlyList<SupplierRoutingCandidateSummaryDto> DeserializeCandidates(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<SupplierRoutingCandidateSummaryDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
