using HAMBOX.SharedKernel.Results;
using MediatR;
using System;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.CreateCategory;

public record CreateCategoryCommand(string NameAr, string NameEn, string Slug) : IRequest<Result<Guid>>;
