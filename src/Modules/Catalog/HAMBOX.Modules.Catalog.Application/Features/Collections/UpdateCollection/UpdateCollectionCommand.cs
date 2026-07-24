using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.UpdateCollection;

public record UpdateCollectionCommand(
    Guid Id,
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    Guid? ParentId,
    int SortOrder) : IRequest<Result>;
