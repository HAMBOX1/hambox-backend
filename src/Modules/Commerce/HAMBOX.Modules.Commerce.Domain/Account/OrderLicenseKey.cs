using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// Represents a license key issued for an order line item.
/// </summary>
public sealed class OrderLicenseKey : Entity
{
    private OrderLicenseKey()
    {
    }

    private OrderLicenseKey(
        Guid id,
        Guid orderId,
        Guid orderItemId,
        Guid productId,
        Guid? productVariantId,
        Guid? digitalInventoryCodeId,
        string licenseKey)
        : base(id)
    {
        OrderId = orderId;
        OrderItemId = orderItemId;
        ProductId = productId;
        ProductVariantId = productVariantId;
        DigitalInventoryCodeId = digitalInventoryCodeId;
        LicenseKey = licenseKey;
        LicenseKeyHash = Hash(licenseKey);
    }

    /// <summary>
    /// Gets the order identifier.
    /// </summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Gets the order item identifier.
    /// </summary>
    public Guid OrderItemId { get; private set; }

    /// <summary>
    /// Gets the product identifier.
    /// </summary>
    public Guid ProductId { get; private set; }

    public Guid? ProductVariantId { get; private set; }

    public Guid? DigitalInventoryCodeId { get; private set; }

    /// <summary>
    /// Gets the license key value.
    /// </summary>
    public string LicenseKey { get; private set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the plaintext license key, used for exact-match lookups now that
    /// <see cref="LicenseKey"/> is encrypted at rest and can no longer be searched in SQL.
    /// </summary>
    public string LicenseKeyHash { get; private set; } = string.Empty;

    /// <summary>
    /// Creates a license key for an order item.
    /// </summary>
    public static OrderLicenseKey Create(
        Guid orderId,
        Guid orderItemId,
        Guid productId,
        string licenseKey,
        Guid? productVariantId = null,
        Guid? digitalInventoryCodeId = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order identifier must not be empty.", nameof(orderId));
        }

        if (orderItemId == Guid.Empty)
        {
            throw new ArgumentException("Order item identifier must not be empty.", nameof(orderItemId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product identifier must not be empty.", nameof(productId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(licenseKey);

        return new OrderLicenseKey(
            Guid.NewGuid(),
            orderId,
            orderItemId,
            productId,
            productVariantId,
            digitalInventoryCodeId,
            licenseKey);
    }

    public static string Hash(string licenseKey) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(licenseKey.Trim().ToUpperInvariant())));
}
