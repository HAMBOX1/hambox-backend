namespace HAMBOX.Application.Abstractions;

/// <summary>
/// Supplies USD-based exchange rates for supported display currencies.
/// </summary>
public interface ICurrencyExchangeRateProvider
{
    /// <summary>
    /// Fetches the latest exchange rates keyed by ISO 4217 currency code.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rates where each value represents how many units of the currency equal one USD.</returns>
    Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken = default);
}
