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
        DateTimeOffset expiresOnUtc)
        : base(id)
    {
        CodeId = codeId;
        VariantId = variantId;
        UserId = userId;
        CartId = cartId;
        ExpiresOnUtc = expiresOnUtc;
        IsActive = true;
    }

    public Guid CodeId { get; private set; }
    public Guid VariantId { get; private set; }
    public string? UserId { get; private set; }
    public Guid? CartId { get; private set; }
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
            DateTimeOffset.UtcNow.AddMinutes(reservationMinutes));
    }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresOnUtc;

    public void Release()
    {
        IsActive = false;
        ReleasedOnUtc = DateTimeOffset.UtcNow;
    }

    public void Complete()
    {
        IsActive = false;
        ReleasedOnUtc = DateTimeOffset.UtcNow;
    }
}
