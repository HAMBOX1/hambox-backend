using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Errors;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Suppliers.Application.Features.Suppliers;

// ─── Query: Availability Summary ─────────────────────────────────

public sealed record GetSupplierAvailabilitySummaryQuery(Guid SupplierId) : IRequest<Result<SupplierAvailabilitySummaryDto>>;

internal sealed class GetSupplierAvailabilitySummaryQueryHandler(ISuppliersDbContext dbContext)
    : IRequestHandler<GetSupplierAvailabilitySummaryQuery, Result<SupplierAvailabilitySummaryDto>>
{
    public async Task<Result<SupplierAvailabilitySummaryDto>> Handle(GetSupplierAvailabilitySummaryQuery request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Suppliers.AsNoTracking().AnyAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<SupplierAvailabilitySummaryDto>(SupplierErrors.NotFound);
        }

        var mappedCount = await dbContext.SupplierProductMappings.AsNoTracking()
            .CountAsync(m => m.SupplierId == request.SupplierId && m.Status == SupplierMappingStatus.Active, cancellationToken);

        var rows = await dbContext.SupplierProductAvailabilities.AsNoTracking()
            .Where(a => a.SupplierId == request.SupplierId)
            .Select(a => new { a.AvailabilityState, a.LastCheckedAtUtc })
            .ToListAsync(cancellationToken);

        var available = rows.Count(r => r.AvailabilityState == HAMBOX.Modules.Suppliers.Domain.Suppliers.SupplierAvailabilityState.Available);
        var unavailable = rows.Count(r => r.AvailabilityState == HAMBOX.Modules.Suppliers.Domain.Suppliers.SupplierAvailabilityState.Unavailable);
        // Mappings with no row at all are Unknown too — never synced yet.
        var unknown = mappedCount - available - unavailable;
        var lastSyncedAtUtc = rows.Count > 0 ? rows.Max(r => r.LastCheckedAtUtc) : null;

        return Result.Success(new SupplierAvailabilitySummaryDto(mappedCount, available, unavailable, Math.Max(0, unknown), lastSyncedAtUtc));
    }
}

// ─── Command: Sync Now ─────────────────────────────────

public sealed record SyncSupplierAvailabilityCommand(Guid SupplierId) : IRequest<Result<SupplierAvailabilitySyncResult>>;

internal sealed class SyncSupplierAvailabilityCommandHandler(
    ISuppliersDbContext dbContext, ISupplierAvailabilityService availabilityService, ICurrentUserService currentUser)
    : IRequestHandler<SyncSupplierAvailabilityCommand, Result<SupplierAvailabilitySyncResult>>
{
    public async Task<Result<SupplierAvailabilitySyncResult>> Handle(SyncSupplierAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Suppliers.AsNoTracking().AnyAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<SupplierAvailabilitySyncResult>(SupplierErrors.NotFound);
        }

        // The sync itself already writes the SupplierAuditLog row and saves its own changes — passing
        // currentUser.UserId here is what attributes that row to the admin who clicked "Sync now"
        // rather than leaving it null (the recurring background job's own signature).
        var result = await availabilityService.SyncSupplierAsync(request.SupplierId, currentUser.UserId, cancellationToken);
        return Result.Success(result);
    }
}
