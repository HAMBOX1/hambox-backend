using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;

public sealed record AddCartItemCommand(Guid ProductId, int Quantity, string? GuestSessionId, Guid? ProductVariantId = null)
    : IRequest<Result<Contracts.CartDto>>;
