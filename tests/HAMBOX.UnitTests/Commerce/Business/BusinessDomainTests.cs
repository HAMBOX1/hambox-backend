using HAMBOX.Modules.Commerce.Application.Features.Business;
using HAMBOX.Modules.Commerce.Domain.Business;

namespace HAMBOX.UnitTests.Commerce.Business;

public sealed class BusinessDomainTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid Acme = Guid.NewGuid();
    private static readonly Guid Globex = Guid.NewGuid();
    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Variant = Guid.NewGuid();

    [Fact]
    public void Two_accounts_get_different_prices_for_the_same_sku()
    {
        var prices = new[]
        {
            BusinessPrice.Create(null, Product, null, 1, 10m, null, null),
            BusinessPrice.Create(Acme, Product, null, 1, 8m, null, null),
            BusinessPrice.Create(Globex, Product, null, 1, 9m, null, null),
        };

        Assert.Equal(8m, BusinessPricing.Resolve(prices, Acme, Product, Variant, 1, Now)!.UnitPriceUsd);
        Assert.Equal(9m, BusinessPricing.Resolve(prices, Globex, Product, Variant, 1, Now)!.UnitPriceUsd);
        Assert.Equal(10m, BusinessPricing.Resolve(prices, Guid.NewGuid(), Product, Variant, 1, Now)!.UnitPriceUsd);
    }

    [Fact]
    public void Higher_quantity_tier_applies_only_from_its_minimum_and_expired_prices_are_ignored()
    {
        var prices = new[]
        {
            BusinessPrice.Create(Acme, Product, null, 1, 10m, null, null),
            BusinessPrice.Create(Acme, Product, null, 50, 7m, null, null),
            BusinessPrice.Create(Acme, Product, null, 5, 1m, Now.AddDays(-10), Now.AddDays(-1)),
        };

        Assert.Equal(10m, BusinessPricing.Resolve(prices, Acme, Product, Variant, 49, Now)!.UnitPriceUsd);
        Assert.Equal(7m, BusinessPricing.Resolve(prices, Acme, Product, Variant, 50, Now)!.UnitPriceUsd);
        Assert.Null(BusinessPricing.Resolve(prices, Acme, Guid.NewGuid(), Variant, 1, Now));
    }

    [Fact]
    public void Variant_specific_price_beats_a_product_wide_one()
    {
        var prices = new[]
        {
            BusinessPrice.Create(Acme, Product, null, 1, 10m, null, null),
            BusinessPrice.Create(Acme, Product, Variant, 1, 6m, null, null),
        };

        Assert.Equal(6m, BusinessPricing.Resolve(prices, Acme, Product, Variant, 1, Now)!.UnitPriceUsd);
        Assert.Equal(10m, BusinessPricing.Resolve(prices, Acme, Product, Guid.NewGuid(), 1, Now)!.UnitPriceUsd);
    }

    private static BusinessQuote Quote(DateTimeOffset validUntil) =>
        BusinessQuote.Create("Q-1", Acme, validUntil, null, "sales", [BusinessQuoteLine.Create(Product, Variant, "Game", "SKU", 3, 12.5m)]);

    [Fact]
    public void Quote_total_is_fixed_and_it_can_only_be_answered_while_open()
    {
        var quote = Quote(Now.AddDays(7));
        Assert.Equal(37.5m, quote.Total);
        Assert.False(quote.Accept(Now));

        Assert.True(quote.Send());
        Assert.True(quote.Accept(Now));
        Assert.False(quote.Decline(Now));
        Assert.True(quote.MarkOrdered(Guid.NewGuid()));
        Assert.False(quote.MarkOrdered(Guid.NewGuid()));
    }

    [Fact]
    public void A_quote_past_its_date_reads_as_expired_and_cannot_be_accepted()
    {
        var quote = Quote(Now.AddDays(-1));
        quote.Send();

        Assert.Equal(BusinessQuoteStatus.Expired, quote.EffectiveStatus(Now));
        Assert.False(quote.Accept(Now));
    }

    [Fact]
    public void Approval_is_required_and_resubmitting_after_a_decision_goes_back_to_review()
    {
        var account = BusinessAccount.Apply("u1", "Acme Ltd", "TAX1", "eg", null, null, null);
        Assert.False(account.IsApproved);
        Assert.False(account.StarsEligible);

        account.Decide(BusinessAccountStatus.Approved, "staff", null);
        Assert.True(account.IsApproved);

        account.UpdateApplication("Acme Trading", "TAX1", "EG", null, null, null);
        Assert.Equal(BusinessAccountStatus.Pending, account.Status);
    }
}
