using HAMBOX.Infrastructure.Currency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Infrastructure.Currency;

/// <summary>
/// Regional preference endpoints (currencies and exchange rates).
/// </summary>
public static class LocalizationEndpointExtensions
{
    private static readonly IReadOnlyList<CurrencyInfoDto> SupportedCurrencies =
    [
        new("USD", "US Dollar", "$", "🇺🇸"),
        new("EUR", "Euro", "€", "🇪🇺"),
        new("EGP", "Egyptian Pound", "E£", "🇪🇬"),
        new("SAR", "Saudi Riyal", "﷼", "🇸🇦"),
    ];

    /// <summary>Maps localization endpoints.</summary>
    public static IEndpointRouteBuilder MapLocalizationEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/localization")
            .WithTags("Localization");

        group.MapGet("/currencies", () => Results.Ok(SupportedCurrencies));

        group.MapGet("/exchange-rates", async (
            CurrencyExchangeRateService ratesService,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await ratesService.GetRatesAsync(cancellationToken);
            return Results.Ok(new ExchangeRatesResponseDto(
                snapshot.BaseCurrency,
                snapshot.Rates,
                snapshot.UpdatedAtUtc));
        });

        return builder;
    }
}

/// <summary>Supported currency metadata.</summary>
public sealed record CurrencyInfoDto(string Code, string Name, string Symbol, string FlagEmoji);

/// <summary>Exchange rates API response.</summary>
public sealed record ExchangeRatesResponseDto(
    string BaseCurrency,
    IReadOnlyDictionary<string, decimal> Rates,
    DateTimeOffset UpdatedAtUtc);
