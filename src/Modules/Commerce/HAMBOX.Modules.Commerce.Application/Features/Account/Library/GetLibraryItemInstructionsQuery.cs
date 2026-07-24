using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Library;

public sealed record GetLibraryItemInstructionsQuery(Guid OrderItemId) : IRequest<Result<LibraryItemInstructionsDto>>;
