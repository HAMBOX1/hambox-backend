using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Library;

public sealed record RevealCustomerLibraryKeyQuery(Guid LibraryItemId)
    : IRequest<Result<RevealCustomerLibraryKeyDto>>;

internal sealed class RevealCustomerLibraryKeyQueryHandler
    : IRequestHandler<RevealCustomerLibraryKeyQuery, Result<RevealCustomerLibraryKeyDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public RevealCustomerLibraryKeyQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<RevealCustomerLibraryKeyDto>> Handle(
        RevealCustomerLibraryKeyQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<RevealCustomerLibraryKeyDto>(CommerceErrors.AuthenticationRequired);
        }

        var key = await (
            from license in _commerceDbContext.OrderLicenseKeys.AsNoTracking()
            join order in _commerceDbContext.Orders.AsNoTracking() on license.OrderId equals order.Id
            where license.Id == request.LibraryItemId
                  && order.UserId == _currentUserService.UserId
            select license)
            .FirstOrDefaultAsync(cancellationToken);

        if (key is null || string.IsNullOrWhiteSpace(key.LicenseKey))
        {
            return Result.Failure<RevealCustomerLibraryKeyDto>(CommerceErrors.OrderLicenseKeyNotFound);
        }

        return Result.Success(new RevealCustomerLibraryKeyDto(key.LicenseKey));
    }
}
