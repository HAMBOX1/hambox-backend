using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.ClearCart;

public sealed record ClearCartCommand(string? GuestSessionId) : IRequest<Result>;
