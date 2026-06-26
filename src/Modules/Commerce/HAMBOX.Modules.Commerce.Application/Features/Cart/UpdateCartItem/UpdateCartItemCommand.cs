using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.UpdateCartItem;

public sealed record UpdateCartItemCommand(Guid ProductId, int Quantity, string? GuestSessionId)
    : IRequest<Result<Contracts.CartDto>>;
