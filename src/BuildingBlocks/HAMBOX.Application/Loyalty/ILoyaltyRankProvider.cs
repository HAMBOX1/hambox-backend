namespace HAMBOX.Application.Loyalty;

/// <summary>
/// Lets other modules (Themes) ask which loyalty rank should style a customer's storefront, without depending on
/// Commerce. Backed by Commerce's loyalty service.
/// </summary>
public interface ILoyaltyRankProvider
{
    /// <summary>
    /// The customer's rank key (bronze…diamond) when the Loyalty Ranks and Rank Themes features are on and the rank is above
    /// Normal and visible; otherwise null (the original HAMBOX design applies).
    /// </summary>
    Task<string?> GetThemeRankKeyAsync(string userId, CancellationToken cancellationToken = default);
}
