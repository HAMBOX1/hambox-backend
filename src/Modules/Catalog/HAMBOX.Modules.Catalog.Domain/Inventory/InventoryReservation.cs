using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

public sealed class InventoryReservation : Entity
{
    public const int DefaultReservationMinutes = 15;

    private InventoryReservation()
    {
    }

    private InventoryReservation(
        Guid id,
        Guid codeId,
        Guid variantId,
        string? userId,
        Guid? cartId,
        DateTimeOffset expiresOnUtc,
        Guid? orderId)
        : base(id)
    {
        CodeId = codeId;
        VariantId = variantId;
        UserId = userId;
        CartId = cartId;
        ExpiresOnUtc = expiresOnUtc;
        OrderId = orderId;
        IsActive = true;
    }

    public Guid CodeId { get; private set; }
    public Guid VariantId { get; private set; }
    public string? UserId { get; private set; }
    public Guid? CartId { get; private set; }

    /// <summary>Set when the hold belongs to a pending (unpaid) order — a hosted-gateway checkout holding stock while the customer pays.</summary>
    public Guid? OrderId { get; private set; }

    /// <summary>Why the hold ended (expired, payment failed, cancelled, used by the order). Kept for the audit trail.</summary>
    public string? ReleaseReason { get; private set; }
    public DateTimeOffset ExpiresOnUtc { get; private set; }
    public DateTimeOffset? ReleasedOnUtc { get; private set; }
    public bool IsActive { get; private set; }

    public static InventoryReservation Create(
        Guid codeId,
        Guid variantId,
        string? userId,
        Guid? cartId,
        int reservationMinutes = DefaultReservationMinutes)
    {
        return new InventoryReservation(
            Guid.NewGuid(),
            codeId,
            variantId,
            userId,
            cartId,
            DateTimeOffset.UtcNow.AddMinutes(reservationMinutes),
            orderId: null);
    }

    /// <summary>A hold taken for a pending order: keeps the code until <paramref name="expiresOnUtc"/> while the customer pays.</summary>
    public static InventoryReservation CreateForOrder(
        Guid codeId,
        Guid variantId,
        string? userId,
        Guid orderId,
        DateTimeOffset expiresOnUtc) =>
        new(Guid.NewGuid(), codeId, variantId, userId, cartId: null, expiresOnUtc, orderId);

    /// <summary>Pushes the expiry out (never earlier) — used when the order is paid and fulfillment is about to consume the hold.</summary>
    public void ExtendTo(DateTimeOffset expiresOnUtc)
    {
        if (expiresOnUtc > ExpiresOnUtc)
        {
            ExpiresOnUtc = expiresOnUtc;
        }
    }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresOnUtc;

    public void Release(string? reason = null)
    {
        IsActive = false;
        ReleasedOnUtc = DateTimeOffset.UtcNow;
        ReleaseReason = reason;
    }

    public void Complete()
    {
        IsActive = false;
        ReleasedOnUtc = DateTimeOffset.UtcNow;
        ReleaseReason = "Consumed by order";
    }
}
