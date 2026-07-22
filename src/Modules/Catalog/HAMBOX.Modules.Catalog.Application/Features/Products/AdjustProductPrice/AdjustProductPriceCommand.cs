using System;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.AdjustProductPrice;

public sealed record AdjustProductPriceCommand(Guid ProductId, PriceAdjustmentMode Mode, decimal Value) : IRequest<Result>;
