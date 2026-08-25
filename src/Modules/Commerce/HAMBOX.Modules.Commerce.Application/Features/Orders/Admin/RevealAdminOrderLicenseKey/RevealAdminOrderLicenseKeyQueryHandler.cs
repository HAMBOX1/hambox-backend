using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.RevealAdminOrderLicenseKey;

public sealed record RevealAdminOrderLicenseKeyQuery(Guid OrderId, Guid LicenseKeyId)
    : IRequest<Result<RevealAdminOrderLicenseKeyDto>>;

internal sealed class RevealAdminOrderLicenseKeyQueryHandler
    : IRequestHandler<RevealAdminOrderLicenseKeyQuery, Result<RevealAdminOrderLicenseKeyDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public RevealAdminOrderLicenseKeyQueryHandler(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<RevealAdminOrderLicenseKeyDto>> Handle(
        RevealAdminOrderLicenseKeyQuery request,
        CancellationToken cancellationToken)
    {
        var key = await _dbContext.OrderLicenseKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(
                k => k.Id == request.LicenseKeyId && k.OrderId == request.OrderId,
                cancellationToken);

        if (key is null)
        {
            return Result.Failure<RevealAdminOrderLicenseKeyDto>(CommerceErrors.OrderLicenseKeyNotFound);
        }

        var actorId = _currentUserService.UserId ?? "system";
        var actorName = _currentUserService.DisplayName ?? actorId;
        _dbContext.OrderAuditEntries.Add(OrderAuditEntry.Create(
            request.OrderId,
            "LicenseKeyRevealed",
            "An administrator revealed a license key.",
            actorId,
            actorName));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new RevealAdminOrderLicenseKeyDto(key.LicenseKey));
    }
}