using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Wishlist.RemoveWishlistItem;

public sealed record RemoveWishlistItemCommand(Guid ProductId) : IRequest<Result>;

internal sealed class RemoveWishlistItemCommandHandler : IRequestHandler<RemoveWishlistItemCommand, Result>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public RemoveWishlistItemCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(RemoveWishlistItemCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure(CommerceErrors.AuthenticationRequired);
        }

        var item = await _commerceDbContext.WishlistItems
            .FirstOrDefaultAsync(
                w => w.UserId == _currentUserService.UserId && w.ProductId == request.ProductId,
                cancellationToken);

        if (item is null)
        {
            return Result.Failure(CommerceErrors.WishlistItemNotFound);
        }

        _commerceDbContext.WishlistItems.Remove(item);
        await _commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
