using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart;

public sealed record GetCartQuery(string? GuestSessionId) : IRequest<Result<Contracts.CartDto>>;
