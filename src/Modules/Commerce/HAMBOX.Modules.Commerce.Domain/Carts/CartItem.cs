using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Carts;

/// <summary>
/// Represents a line item in a shopping cart.
/// </summary>
public sealed class CartItem : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CartItem"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private CartItem()
    {
    }

    private CartItem(Guid id, Guid shoppingCartId, Guid productId, int quantity, decimal unitPrice)
        : base(id)
    {
        ShoppingCartId = shoppingCartId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    /// <summary>
    /// Gets the shopping cart identifier.
    /// </summary>
    public Guid ShoppingCartId { get; private set; }

    /// <summary>
    /// Gets the product identifier.
    /// </summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Gets the quantity in the cart.
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Gets the unit price snapshot at the time the item was added.
    /// </summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>
    /// Creates a new cart item.
    /// </summary>
    internal static CartItem Create(Guid shoppingCartId, Guid productId, int quantity, decimal unitPrice)
    {
        if (shoppingCartId == Guid.Empty)
        {
            throw new ArgumentException("Shopping cart identifier must not be empty.", nameof(shoppingCartId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product identifier must not be empty.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

        return new CartItem(Guid.NewGuid(), shoppingCartId, productId, quantity, unitPrice);
    }

    /// <summary>
    /// Updates the quantity and unit price snapshot.
    /// </summary>
    internal void Update(int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
