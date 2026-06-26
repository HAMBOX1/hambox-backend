using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Storefront.GetStorefrontContent;

public sealed record GetStorefrontContentQuery : IRequest<Result<StorefrontContentDto>>;
