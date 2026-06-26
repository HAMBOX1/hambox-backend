using HAMBOX.SharedKernel.Results;
using MediatR;
using System;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.UpdateCategory;

public record UpdateCategoryCommand(Guid Id, string NameAr, string NameEn, string Slug, bool IsActive) : IRequest<Result>;
