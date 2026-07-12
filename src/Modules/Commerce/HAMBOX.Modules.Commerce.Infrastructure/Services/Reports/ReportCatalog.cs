using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Reports;
using HAMBOX.Modules.Commerce.Domain.Reports;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services.Reports;

internal sealed class ReportCatalog : IReportCatalog
{
    private static readonly IReadOnlyList<ReportTypeInfoDto> Types =
    [
        new(ReportTypes.Sales, "Sales", "Sales performance overview from completed orders.", "Commerce", ReportFormats.All),
        new(ReportTypes.Revenue, "Revenue", "Gross, net, pending, and refunded revenue.", "Commerce", ReportFormats.All),
        new(ReportTypes.Orders, "Orders", "Order volumes and status breakdown.", "Commerce", ReportFormats.All),
        new(ReportTypes.Inventory, "Inventory", "Stock levels, value, and low-stock variants.", "Catalog", ReportFormats.All),
        new(ReportTypes.Products, "Products", "Top and underperforming products.", "Catalog", ReportFormats.All),
        new(ReportTypes.Categories, "Categories", "Category revenue and quantity.", "Catalog", ReportFormats.All),
        new(ReportTypes.Membership, "Membership", "Membership plans, MRR, and subscriptions.", "Commerce", ReportFormats.All),
        new(ReportTypes.Promotion, "Promotion", "Promotion redemptions and discounts.", "Commerce", ReportFormats.All),
        new(ReportTypes.Coupon, "Coupon", "Coupon usage and discount totals.", "Commerce", ReportFormats.All),
        new(ReportTypes.Referral, "Referral", "Referral invites and conversions.", "Commerce", ReportFormats.All),
        new(ReportTypes.Customer, "Customer", "New vs returning customers.", "Commerce", ReportFormats.All),
        new(ReportTypes.Operations, "Operations", "Jobs, API health, and operational alerts.", "Operations", ReportFormats.All),
        new(ReportTypes.Audit, "Audit", "Recent operational audit log entries.", "Operations", ReportFormats.All),
    ];

    public IReadOnlyList<ReportTypeInfoDto> GetTypes() => Types;

    public ReportTypeInfoDto? GetType(string reportType) =>
        Types.FirstOrDefault(t => string.Equals(t.Type, reportType, StringComparison.OrdinalIgnoreCase));
}
