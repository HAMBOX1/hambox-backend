namespace HAMBOX.Modules.Commerce.Application.Contracts.Orders;

public sealed record AdminOrderListItemDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string Email,
    int ItemsCount,
    string ProductsSummary,
    decimal OrderTotal,
    string PaymentMethod,
    string PaymentStatus,
    string OrderStatus,
    string DeliveryStatus,
    decimal MembershipDiscount,
    string? CouponCode,
    DateTimeOffset PurchaseDate,
    string? LastEditedByName,
    DateTimeOffset? LastEditedOnUtc);

public sealed record AdminOrderStatisticsDto(
    int TodaysOrders,
    int PendingOrders,
    decimal RevenueToday,
    decimal AverageOrderValue,
    int Refunds);

public sealed record AdminOrderDetailDto(
    Guid Id,
    string OrderNumber,
    string OrderStatus,
    string PaymentStatus,
    string DeliveryStatus,
    string Email,
    string CustomerName,
    string Country,
    string PaymentMethod,
    string? PaymentProvider,
    string? PaymentTransactionId,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal MembershipDiscount,
    decimal TaxAmount,
    decimal TotalAmount,
    string? CouponCode,
    string? CouponPromotionName,
    IReadOnlyList<AdminOrderItemDto> Items,
    IReadOnlyList<AdminOrderTimelineEventDto> Timeline,
    IReadOnlyList<AdminOrderLicenseKeyDto> LicenseKeys,
    IReadOnlyList<AdminOrderAdminNoteDto> AdminNotes,
    IReadOnlyList<AdminOrderAuditEntryDto> AuditHistory,
    IReadOnlyList<AdminOrderPaymentCallbackDto> PaymentCallbacks,
    IReadOnlyList<AdminCustomerOrderHistoryItemDto> CustomerOrderHistory,
    string? InvoiceUrl,
    DateTimeOffset CreatedOnUtc,
    string? LastEditedByName,
    DateTimeOffset? LastEditedOnUtc,
    IReadOnlyList<AdminOrderSupplierRoutingDto> SupplierRouting);

/// <summary>
/// One automated-supplier routing decision for one order item, admin-visible only — see
/// <c>SupplierRoutingAuditLog</c>'s own remarks for why this is safe for an admin to see (acquisition
/// cost, never the customer's selling price) but must never be exposed anywhere customer-facing.
/// </summary>
public sealed record AdminOrderSupplierRoutingDto(
    Guid OrderItemId,
    string? SelectedSupplierName,
    decimal? SelectedCostInBaseCurrency,
    string BaseCurrency,
    bool FallbackOccurred,
    IReadOnlyList<AdminOrderSupplierRoutingCandidateDto> Candidates,
    DateTimeOffset CreatedOnUtc);

public sealed record AdminOrderSupplierRoutingCandidateDto(
    string SupplierName,
    string ProviderType,
    bool Eligible,
    bool Selected,
    decimal? CostInBaseCurrency,
    string? OriginalCurrency,
    decimal? OriginalCost,
    string? RejectionReason);

public sealed record AdminOrderItemDto(
    Guid Id,
    Guid ProductId,
    Guid? ProductVariantId,
    string ProductName,
    string? VariantSku,
    string? ProductImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal,
    int DeliveredCodesCount,
    string DeliveryStatus);

public sealed record AdminOrderTimelineEventDto(
    string EventType,
    string Description,
    DateTimeOffset OccurredOnUtc);

public sealed record AdminOrderLicenseKeyDto(
    Guid Id,
    Guid OrderItemId,
    Guid ProductId,
    string ProductName,
    string MaskedKey,
    string DeliveryStatus,
    DateTimeOffset? DeliveredOnUtc,
    bool CanReveal);

public sealed record AdminOrderAdminNoteDto(
    Guid Id,
    string Body,
    string AuthorDisplayName,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ModifiedOnUtc);

public sealed record AdminOrderAuditEntryDto(
    Guid Id,
    string EventType,
    string Description,
    string? ActorDisplayName,
    DateTimeOffset OccurredOnUtc);

public sealed record UpdateAdminOrderStatusRequest(string Status);

public sealed record UpsertAdminOrderNoteRequest(string Body);

public sealed record RevealAdminOrderLicenseKeyDto(string LicenseKey);

public sealed record AdminOrderPaymentCallbackDto(
    Guid Id,
    string Provider,
    string EventType,
    string Status,
    string? TransactionId,
    string PayloadJson,
    DateTimeOffset OccurredOnUtc);

public sealed record AdminCustomerOrderHistoryItemDto(
    Guid Id,
    string OrderNumber,
    decimal TotalAmount,
    string OrderStatus,
    string PaymentStatus,
    string DeliveryStatus,
    DateTimeOffset PurchaseDate);

public sealed record BulkAdminOrdersRequest(
    string Action,
    IReadOnlyList<Guid> OrderIds);

public sealed record BulkAdminOrdersResultDto(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> Errors);

public sealed record AssignAdminOrderManualCodeRequest(
    Guid OrderItemId,
    string LicenseKey);

public sealed record RetryAdminOrderFulfillmentResultDto(
    int CodesDelivered,
    bool OrderCompleted);
