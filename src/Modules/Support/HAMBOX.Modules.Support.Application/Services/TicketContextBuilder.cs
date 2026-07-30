using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Services;

/// <summary>
/// Assembles the Customer Context Panel by reading Identity/Commerce/Catalog directly —
/// the same accepted cross-module DbContext-injection pattern Commerce already uses against
/// Catalog (CLAUDE.md §3), applied here for a read-only composition rather than a write.
/// </summary>
public sealed class TicketContextBuilder(
    IIdentityDbContext identityDb,
    ICommerceDbContext commerceDb,
    ICatalogDbContext catalogDb,
    ISupportDbContext supportDb)
{
    public async Task<TicketCustomerContextDto> BuildAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var customer = await UserDisplayResolver.ResolveOneAsync(identityDb, ticket.CustomerUserId, cancellationToken);

        var membership = await commerceDb.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == ticket.CustomerUserId && s.Status == MembershipSubscriptionStatus.Active)
            .OrderByDescending(s => s.ExpiresOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

        string? membershipPlanName = null;
        if (membership is not null)
        {
            membershipPlanName = await commerceDb.MembershipPlans
                .AsNoTracking()
                .Where(p => p.Id == membership.PlanId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

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
            membership?.Status.ToString(),
            membership?.ExpiresOnUtc,
            recentOrderDtos,
            relatedOrderNumber,
            relatedProductName,
            ticket.CustomerCountry,
            ticket.CustomerBrowser,
            ticket.CustomerDevice,
            recentTicketsCount);
    }
}
