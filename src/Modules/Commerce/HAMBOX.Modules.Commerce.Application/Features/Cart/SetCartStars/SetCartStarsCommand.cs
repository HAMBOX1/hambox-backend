using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.SetCartStars;

/// <summary>Chooses how many Stars to spend on the next checkout (0 clears it). The usable amount is re-quoted when totals are built.</summary>
public sealed record SetCartStarsCommand(int Stars) : IRequest<Result>;

internal sealed class SetCartStarsCommandValidator : AbstractValidator<SetCartStarsCommand>
{
    public SetCartStarsCommandValidator() => RuleFor(x => x.Stars).InclusiveBetween(0, 100_000_000);
}

internal sealed class SetCartStarsCommandHandler(ICommerceDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<SetCartStarsCommand, Result>
{
    public async Task<Result> Handle(SetCartStarsCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure(CommerceErrors.AuthenticationRequired);
        }

        var cart = await db.ShoppingCarts.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (cart is null)
        {
            return Result.Failure(CommerceErrors.CartNotFound);
        }

        cart.SetStarsToRedeem(request.Stars);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
