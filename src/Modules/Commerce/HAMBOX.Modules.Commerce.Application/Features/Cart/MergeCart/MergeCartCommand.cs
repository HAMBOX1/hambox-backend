using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Cart.MergeCart;

public sealed record MergeCartCommand(string GuestSessionId) : IRequest<Result<Contracts.CartDto>>;
