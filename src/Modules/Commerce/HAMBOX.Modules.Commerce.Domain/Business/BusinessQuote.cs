using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Business;

public enum BusinessQuoteStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4,
    Ordered = 5,
}

/// <summary>A quoted price for one product line. The price is fixed when the quote is built, so later price-list edits don't change it.</summary>
public sealed class BusinessQuoteLine : Entity
{
    private BusinessQuoteLine()
    {
    }

    private BusinessQuoteLine(Guid id)
        : base(id)
    {
    }

    public Guid QuoteId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public decimal UnitPriceUsd { get; private set; }

    public decimal LineTotal => UnitPriceUsd * Quantity;

    public static BusinessQuoteLine Create(Guid productId, Guid variantId, string productName, string sku, int quantity, decimal unitPriceUsd)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(unitPriceUsd, 0m);
        return new BusinessQuoteLine(Guid.NewGuid())
        {
            ProductId = productId,
            ProductVariantId = variantId,
            ProductName = productName,
            Sku = sku,
            Quantity = quantity,
            UnitPriceUsd = decimal.Round(unitPriceUsd, 2),
        };
    }
}

/// <summary>
/// A time-limited offer from sales to a business account. The customer accepts or declines it; sales then turns an accepted
/// quote into an order (<see cref="OrderId"/>). A quote promises neither stock nor distribution rights — those are confirmed
/// at fulfilment — and it expires on its own after <see cref="ValidUntilUtc"/>.
/// </summary>
public sealed class BusinessQuote : AggregateRoot
{
    private readonly List<BusinessQuoteLine> _lines = [];

    private BusinessQuote()
    {
    }

    private BusinessQuote(Guid id)
        : base(id)
    {
    }

    public string QuoteNumber { get; private set; } = string.Empty;

    public Guid BusinessAccountId { get; private set; }

    public BusinessQuoteStatus Status { get; private set; }

    public DateTimeOffset ValidUntilUtc { get; private set; }

    public string? Terms { get; private set; }

    public string CreatedByUserId { get; private set; } = string.Empty;

    public DateTimeOffset? RespondedOnUtc { get; private set; }

    public Guid? OrderId { get; private set; }

    public IReadOnlyCollection<BusinessQuoteLine> Lines => _lines.AsReadOnly();

    public decimal Total => _lines.Sum(l => l.LineTotal);

    public static BusinessQuote Create(string number, Guid accountId, DateTimeOffset validUntilUtc, string? terms, string createdBy, IEnumerable<BusinessQuoteLine> lines)
    {
        var quote = new BusinessQuote(Guid.NewGuid())
        {
            QuoteNumber = number,
            BusinessAccountId = accountId,
            ValidUntilUtc = validUntilUtc,
            Terms = string.IsNullOrWhiteSpace(terms) ? null : terms.Trim(),
            CreatedByUserId = createdBy,
        };
        quote._lines.AddRange(lines);
        if (quote._lines.Count == 0)
        {
            throw new ArgumentException("A quote needs at least one line.", nameof(lines));
        }

        return quote;
    }

    public bool IsOpenAt(DateTimeOffset now) => Status == BusinessQuoteStatus.Sent && ValidUntilUtc > now;

    public bool Send() => Transition(BusinessQuoteStatus.Draft, BusinessQuoteStatus.Sent);

    public bool Accept(DateTimeOffset now) => IsOpenAt(now) && Transition(BusinessQuoteStatus.Sent, BusinessQuoteStatus.Accepted);

    public bool Decline(DateTimeOffset now) => IsOpenAt(now) && Transition(BusinessQuoteStatus.Sent, BusinessQuoteStatus.Declined);

    public bool MarkOrdered(Guid orderId)
    {
        if (!Transition(BusinessQuoteStatus.Accepted, BusinessQuoteStatus.Ordered))
        {
            return false;
        }

        OrderId = orderId;
        return true;
    }

    /// <summary>An unanswered sent quote past its date reads as Expired.</summary>
    public BusinessQuoteStatus EffectiveStatus(DateTimeOffset now) =>
        Status == BusinessQuoteStatus.Sent && ValidUntilUtc <= now ? BusinessQuoteStatus.Expired : Status;

    private bool Transition(BusinessQuoteStatus from, BusinessQuoteStatus to)
    {
        if (Status != from)
        {
            return false;
        }

        Status = to;
        RespondedOnUtc = to is BusinessQuoteStatus.Accepted or BusinessQuoteStatus.Declined ? DateTimeOffset.UtcNow : RespondedOnUtc;
        return true;
    }
}
