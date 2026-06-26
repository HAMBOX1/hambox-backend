using HAMBOX.SharedKernel.Results;
using MediatR;
using System;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.DeleteCategory;

public record DeleteCategoryCommand(Guid Id) : IRequest<Result>;
