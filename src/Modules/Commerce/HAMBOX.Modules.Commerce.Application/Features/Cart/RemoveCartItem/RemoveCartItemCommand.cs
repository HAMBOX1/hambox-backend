using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.RemoveCartItem;

public sealed record RemoveCartItemCommand(Guid ProductId, string? GuestSessionId, Guid? ProductVariantId = null)
    : IRequest<Result<Contracts.CartDto>>;
