using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Archive;

public enum LegacyLinkStatus
{
    Unlinked = 0,
    Linked = 1,
}

/// <summary>
/// A customer carried over from the old HAMBOX archive. It stays <see cref="LegacyLinkStatus.Unlinked"/> until the real
/// owner is established (their verified e-mail matches, or staff link it with a recorded reason) — knowing an old customer
/// number is never enough to claim someone's history. Re-importing the same <see cref="LegacyId"/> never creates a second record.
/// </summary>
public sealed class LegacyCustomerRecord : Entity
{
    private LegacyCustomerRecord()
    {
    }

    private LegacyCustomerRecord(Guid id)
        : base(id)
    {
    }

    public string LegacyId { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Name { get; private set; }

    public DateTimeOffset? RegisteredOnUtc { get; private set; }

    /// <summary>Stars the old programme documented as owed to this customer. Credited once, on linking — never recomputed from history.</summary>
    public int PromisedStars { get; private set; }

    public LegacyLinkStatus Status { get; private set; }

    public string? LinkedUserId { get; private set; }

    public DateTimeOffset? LinkedOnUtc { get; private set; }

    public string? LinkedBy { get; private set; }

    public string? LinkReason { get; private set; }

    public DateTimeOffset ImportedOnUtc { get; private set; }

    public static LegacyCustomerRecord Create(
        string legacyId, string? email, string? phone, string? name, DateTimeOffset? registeredOnUtc, int promisedStars) =>
        new(Guid.NewGuid())
        {
            LegacyId = legacyId.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            RegisteredOnUtc = registeredOnUtc,
            PromisedStars = Math.Max(promisedStars, 0),
            ImportedOnUtc = DateTimeOffset.UtcNow,
        };

    public bool Link(string userId, string linkedBy, string reason)
    {
        if (Status == LegacyLinkStatus.Linked)
        {
            return false;
        }

        Status = LegacyLinkStatus.Linked;
        LinkedUserId = userId;
        LinkedOnUtc = DateTimeOffset.UtcNow;
        LinkedBy = linkedBy;
        LinkReason = reason;
        return true;
    }
}

/// <summary>One old order, kept as history on the customer's profile. It never touches stock, payments or the new order flow.</summary>
public sealed class LegacyOrderRecord : Entity
{
    private LegacyOrderRecord()
    {
    }

    private LegacyOrderRecord(Guid id)
        : base(id)
    {
    }

    public string LegacyOrderId { get; private set; } = string.Empty;

    public string LegacyCustomerId { get; private set; } = string.Empty;

    public decimal TotalAmount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public string Status { get; private set; } = string.Empty;

    public DateTimeOffset? OrderedOnUtc { get; private set; }

    public static LegacyOrderRecord Create(
        string legacyOrderId, string legacyCustomerId, decimal total, string? currency, string? status, DateTimeOffset? orderedOnUtc) =>
        new(Guid.NewGuid())
        {
            LegacyOrderId = legacyOrderId.Trim(),
            LegacyCustomerId = legacyCustomerId.Trim(),
            TotalAmount = total,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            Status = status?.Trim() ?? string.Empty,
            OrderedOnUtc = orderedOnUtc,
        };
}
