using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

public enum CustomerAlertType
{
    BackInStock = 0,
    PriceDrop = 1,
}

/// <summary>
/// One customer's subscription to be notified about a single <see cref="ProductVariant"/> — the
/// actual purchasable entity, never the parent product alone (a product's variants can have
/// independent stock and independent prices). Owned by either an authenticated <see cref="UserId"/>
/// or an anonymous <see cref="GuestSessionId"/>, mirroring <c>ShoppingCart</c>'s dual identity; a
/// guest-created row is reassigned to a user via <see cref="ClaimFor"/> the same way
/// <c>MergeCartCommandHandler</c> claims a guest cart on login.
///
/// Fires once: <see cref="MarkNotified"/> deactivates the subscription in the same call, so a
/// restock or price drop notifies a customer exactly one time per subscription. Re-subscribing
/// after that is a new row, not a reactivation — see the architecture audit's back-in-stock/price-drop
/// lifecycle recommendation.
/// </summary>
public sealed class CustomerAlertSubscription : Entity, IAuditable
{
    private CustomerAlertSubscription()
    {
    }

    private CustomerAlertSubscription(
        Guid id,
        string? userId,
        string? guestSessionId,
        CustomerAlertType alertType,
        Guid variantId,
        Guid productId,
        decimal? lastObservedPrice)
        : base(id)
    {
        UserId = userId;
        GuestSessionId = guestSessionId;
        AlertType = alertType;
        VariantId = variantId;
        ProductId = productId;
        LastObservedPrice = lastObservedPrice;
        IsActive = true;
    }

    public string? UserId { get; private set; }
    public string? GuestSessionId { get; private set; }
    public CustomerAlertType AlertType { get; private set; }
    public Guid VariantId { get; private set; }
    public Guid ProductId { get; private set; }

    /// <summary>Only meaningful for <see cref="CustomerAlertType.PriceDrop"/> — the effective price
    /// (<c>Variant.PriceOverride ?? Product.Price</c>) observed when the subscription was created,
    /// compared against the current effective price on each scan pass. Null for BackInStock rows.</summary>
    public decimal? LastObservedPrice { get; private set; }

    public bool IsActive { get; private set; }
    public DateTimeOffset? NotifiedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static CustomerAlertSubscription CreateForUser(
        string userId, CustomerAlertType alertType, Guid variantId, Guid productId, decimal? lastObservedPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new CustomerAlertSubscription(Guid.NewGuid(), userId, null, alertType, variantId, productId, lastObservedPrice);
    }

    public static CustomerAlertSubscription CreateForGuest(
        string guestSessionId, CustomerAlertType alertType, Guid variantId, Guid productId, decimal? lastObservedPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guestSessionId);
        return new CustomerAlertSubscription(Guid.NewGuid(), null, guestSessionId, alertType, variantId, productId, lastObservedPrice);
    }

    /// <summary>Reassigns a guest-created subscription to the now-authenticated user, mirroring
    /// <c>ShoppingCart</c>'s guest-to-user claim. Idempotent to call is the caller's responsibility —
    /// this just clears the guest identity and sets the user one.</summary>
    public void ClaimFor(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        UserId = userId;
        GuestSessionId = null;
    }

    /// <summary>Fires once: records when the alert was sent and deactivates the subscription in the
    /// same call, so the scan job's own idempotency check (<c>IsActive AND NotifiedOnUtc IS NULL</c>)
    /// can never see this row again on a repeated or overlapping pass.</summary>
    public void MarkNotified()
    {
        NotifiedOnUtc = DateTimeOffset.UtcNow;
        IsActive = false;
    }
}
