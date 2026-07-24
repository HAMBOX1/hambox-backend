using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.CreateCollection;

public record CreateCollectionCommand(
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    Guid? ParentId,
    int SortOrder) : IRequest<Result<Guid>>;
