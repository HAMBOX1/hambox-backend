using System;
using System.Collections.Generic;
using HAMBOX.Modules.Catalog.Application.Features.Products.AdjustProductPrice;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.BulkProducts;

/// <summary>
/// Action is one of "publish" | "unpublish" | "archive" — no extra fields needed — or
/// "change-category" (needs <see cref="TargetCategoryId"/>) / "adjust-price" (needs
/// <see cref="PriceMode"/> + <see cref="PriceValue"/>) / "assign-collection" | "remove-collection"
/// (needs <see cref="TargetCollectionId"/>) / "set-fulfillment" (needs <see cref="TargetFulfillmentMode"/>, applied to
/// every variant of each product) / "set-margin" (needs <see cref="PriceValue"/> as the margin percent: the sale
/// price becomes cost ÷ (1 − margin)) / "set-badge" (<see cref="BadgeTextEn"/> / <see cref="BadgeTextAr"/>; both empty clears it). Delete and duplicate are deliberately
/// separate endpoints/commands so each can carry its own permission (Delete / Create).
/// </summary>
public sealed record BulkProductsRequest(
    IReadOnlyList<Guid>? ProductIds,
    string? SearchTerm,
    ProductStatus? Status,
    Guid? CategoryId,
    bool SelectAllMatching,
    string Action,
    Guid? TargetCategoryId,
    PriceAdjustmentMode? PriceMode,
    decimal? PriceValue,
    Guid? TargetCollectionId = null,
    FulfillmentMode? TargetFulfillmentMode = null,
    string? BadgeTextEn = null,
    string? BadgeTextAr = null);

public sealed record BulkProductsResultDto(int SuccessCount, int ErrorCount, IReadOnlyList<string> Errors);

public sealed record BulkProductsCommand(BulkProductsRequest Request) : IRequest<Result<BulkProductsResultDto>>;
