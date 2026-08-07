using HAMBOX.Application.Membership;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Services;

/// <summary>
/// Assembles the Customer Context Panel by reading Identity/Catalog directly (the same accepted
/// cross-module DbContext-injection pattern Commerce already uses against Catalog, CLAUDE.md §3)
/// plus Commerce for order history — but membership standing goes through IMembershipAccessProvider
/// rather than querying MembershipSubscriptions/MembershipPlans directly, so this panel can never
/// drift from what checkout/catalog/themes/support-priority already believe about the customer.
/// </summary>
public sealed class TicketContextBuilder(
    IIdentityDbContext identityDb,
    ICommerceDbContext commerceDb,
    ICatalogDbContext catalogDb,
    ISupportDbContext supportDb,
    IMembershipAccessProvider membershipAccess)
{
    public async Task<TicketCustomerContextDto> BuildAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var customer = await UserDisplayResolver.ResolveOneAsync(identityDb, ticket.CustomerUserId, cancellationToken);

        var membership = await membershipAccess.GetAccessInfoAsync(ticket.CustomerUserId, cancellationToken);
        var membershipPlanName = membership.HasActiveMembership ? membership.PlanName : null;
        // SubscriptionId is only set for a real, explicit subscription — not the default-plan
        // fallback — so "Active" here means the same thing the old Status==Active filter meant.
        var membershipStatus = membership.SubscriptionId is not null ? "Active" : null;

        var recentOrders = await commerceDb.Orders
            .AsNoTracking()
            .Where(o => o.UserId == ticket.CustomerUserId)
            .OrderByDescending(o => o.CreatedOnUtc)
            .Take(5)
            .Select(o => new { o.Id, o.OrderNumber, o.TotalAmount, o.Status, o.CreatedOnUtc })
            .ToListAsync(cancellationToken);

        var orderIds = recentOrders.Select(o => o.Id).ToList();

        var itemsByOrder = await commerceDb.OrderItems
            .AsNoTracking()
            .Where(i => orderIds.Contains(i.OrderId))
            .Select(i => new { i.OrderId, i.ProductNameEn })
            .ToListAsync(cancellationToken);

        var licenseOrderIds = (await commerceDb.OrderLicenseKeys
            .AsNoTracking()
            .Where(k => orderIds.Contains(k.OrderId))
            .Select(k => k.OrderId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var recentOrderDtos = recentOrders.Select(o => new TicketContextOrderDto(
            o.Id,
            o.OrderNumber,
            o.TotalAmount,
            o.Status.ToString(),
            o.CreatedOnUtc,
            itemsByOrder.Where(i => i.OrderId == o.Id).Select(i => i.ProductNameEn).ToList(),
            licenseOrderIds.Contains(o.Id)))
            .ToList();

        string? relatedOrderNumber = null;
        if (ticket.RelatedOrderId is Guid relatedOrderId)
        {
            relatedOrderNumber = recentOrders.FirstOrDefault(o => o.Id == relatedOrderId)?.OrderNumber
                ?? await commerceDb.Orders.AsNoTracking()
                    .Where(o => o.Id == relatedOrderId)
                    .Select(o => o.OrderNumber)
                    .FirstOrDefaultAsync(cancellationToken);
        }

        string? relatedProductName = null;
        if (ticket.RelatedProductId is Guid relatedProductId)
        {
            relatedProductName = await catalogDb.Products.AsNoTracking()
                .Where(p => p.Id == relatedProductId)
                .Select(p => p.NameEn)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var recentTicketsCount = await supportDb.Tickets
            .AsNoTracking()
            .CountAsync(t => t.CustomerUserId == ticket.CustomerUserId && t.Id != ticket.Id, cancellationToken);

        return new TicketCustomerContextDto(
            ticket.CustomerUserId,
            customer?.Name ?? "Unknown customer",
            customer?.Email ?? string.Empty,
            membershipPlanName,
            membershipStatus,
            membership.ExpiresOnUtc,
            recentOrderDtos,
            relatedOrderNumber,
            relatedProductName,
            ticket.CustomerCountry,
            ticket.CustomerBrowser,
            ticket.CustomerDevice,
            recentTicketsCount);
    }
}
