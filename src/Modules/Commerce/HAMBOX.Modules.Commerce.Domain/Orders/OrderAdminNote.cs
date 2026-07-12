using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Orders;

/// <summary>
/// Internal admin note attached to an order.
/// </summary>
public sealed class OrderAdminNote : Entity
{
    private OrderAdminNote()
    {
    }

    private OrderAdminNote(Guid id, Guid orderId, string body, string authorUserId, string authorDisplayName)
        : base(id)
    {
        OrderId = orderId;
        Body = body;
        AuthorUserId = authorUserId;
        AuthorDisplayName = authorDisplayName;
    }

    public Guid OrderId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public string AuthorUserId { get; private set; } = string.Empty;

    public string AuthorDisplayName { get; private set; } = string.Empty;

    public static OrderAdminNote Create(Guid orderId, string body, string authorUserId, string authorDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorDisplayName);

        return new OrderAdminNote(Guid.NewGuid(), orderId, body.Trim(), authorUserId, authorDisplayName.Trim());
    }

    public void UpdateBody(string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        Body = body.Trim();
    }
}
