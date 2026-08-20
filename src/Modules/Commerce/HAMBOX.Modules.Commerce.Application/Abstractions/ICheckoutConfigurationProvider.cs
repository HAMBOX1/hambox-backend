namespace HAMBOX.Modules.Commerce.Application.Abstractions;

public interface ICheckoutConfigurationProvider
{
    bool IsDevelopmentCheckoutEnabled { get; }

    /// <summary>Whether DOT checkout has its required non-secret/secret settings populated. Does not guarantee <see cref="IDotPricePointResolver"/> is configured — see that interface for why pricing is a separate, still-unresolved question.</summary>
    bool IsDotCheckoutEnabled { get; }

    /// <summary>Whether DOT Fawry checkout has its required non-secret/secret settings populated. Does not guarantee <see cref="IDotFawryChargeAmountResolver"/> is configured — see that interface for why the charge currency is a separate, still-unresolved question. A distinct DOT product from <see cref="IsDotCheckoutEnabled"/>.</summary>
    bool IsDotFawryCheckoutEnabled { get; }
}
