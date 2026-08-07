using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Memberships;

/// <summary>
/// Grants members of a plan purchase access to an otherwise-restricted product. Stores a raw
/// <see cref="ProductId"/> — Catalog and Commerce are separate schemas with no cross-schema FKs,
/// mirroring how <c>Promotions.PromotionTarget</c> references Catalog products.
/// </summary>
public sealed class MembershipPlanProductAccess : Entity
{
    private MembershipPlanProductAccess()
    {
    }

    private MembershipPlanProductAccess(Guid id, Guid planId, Guid productId)
        : base(id)
    {
        PlanId = planId;
        ProductId = productId;
    }

    public Guid PlanId { get; private set; }
    public Guid ProductId { get; private set; }

    internal static MembershipPlanProductAccess Create(Guid planId, Guid productId) =>
        new(Guid.NewGuid(), planId, productId);
}
