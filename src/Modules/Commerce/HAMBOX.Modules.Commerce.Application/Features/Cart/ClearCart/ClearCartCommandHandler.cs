using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.ClearCart;

internal sealed class ClearCartCommandHandler : IRequestHandler<ClearCartCommand, Result>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public ClearCartCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartResolver.FindCartAsync(
            _commerceDbContext,
            _currentUserService,
            request.GuestSessionId,
            cancellationToken);

        if (cart is null)
        {
            return Result.Failure(CommerceErrors.CartNotFound);
        }

        cart.Clear();
        await CartPersistenceHelper.PrepareForSaveAsync(_commerceDbContext, cart, cancellationToken);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
