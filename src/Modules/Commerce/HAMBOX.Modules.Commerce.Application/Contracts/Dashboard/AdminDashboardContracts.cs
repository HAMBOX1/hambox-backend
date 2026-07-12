namespace HAMBOX.Modules.Commerce.Application.Contracts.Dashboard;

public sealed record AdminDashboardDto(
    AdminDashboardOrderMetricsDto Orders,
    AdminDashboardInventoryMetricsDto Inventory,
    AdminDashboardPromotionMetricsDto Promotions,
    AdminDashboardMembershipMetricsDto Memberships,
    AdminDashboardAlertsDto Alerts,
    IReadOnlyList<AdminDashboardRecentOrderDto> RecentOrders,
    IReadOnlyList<AdminDashboardRecentCustomerDto> RecentCustomers,
    IReadOnlyList<AdminDashboardLowStockItemDto> LowStockItems);

public sealed record AdminDashboardOrderMetricsDto(
    int TodaysOrders,
    int PendingOrders,
    int FailedOrders,
    int ManualReviewOrders,
    decimal RevenueToday,
    decimal TotalRevenue,
    decimal AverageOrderValue,
    int Refunds);

public sealed record AdminDashboardInventoryMetricsDto(
    int LowStockVariants,
    int OutOfStockVariants,
    int AvailableCodes,
    decimal InventoryValue);

public sealed record AdminDashboardPromotionMetricsDto(
    int ActivePromotions,
    int ActiveCoupons,
    int RedemptionsToday);

public sealed record AdminDashboardMembershipMetricsDto(
    int ActiveSubscriptions,
    int ExpiringWithinWeek,
    decimal MonthlyRecurringRevenue);

public sealed record AdminDashboardAlertsDto(
    int FailedOrders,
    int FailedPayments,
    int LowStockVariants,
    int OutOfStockVariants);

public sealed record AdminDashboardRecentOrderDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string Email,
    decimal TotalAmount,
    string OrderStatus,
    string PaymentStatus,
    string DeliveryStatus,
    DateTimeOffset PurchaseDate);

public sealed record AdminDashboardRecentCustomerDto(
    string Email,
    string CustomerName,
    int OrderCount,
    decimal TotalSpent,
    DateTimeOffset LastOrderDate);

public sealed record AdminDashboardLowStockItemDto(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string Sku,
    int AvailableStock,
    int LowStockThreshold,
    bool IsOutOfStock);
