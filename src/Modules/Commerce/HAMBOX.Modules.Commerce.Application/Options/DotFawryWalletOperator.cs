using System.Globalization;

namespace HAMBOX.Modules.Commerce.Application.Options;

/// <summary>
/// The mobile wallets HAMBOX offers through DOT's Fawry Direct Billing product, each identified by
/// its own DOT-confirmed <c>opId</c> — the underlying enum value IS the wire-format <c>opId</c>, so
/// there is exactly one place these numbers are ever written down. <see cref="DotFawrySettings"/>'s
/// shared credentials (<c>PartnerId</c>/<c>ServiceId</c>/<c>Username</c>/<c>Password</c>) are
/// identical across all three wallets — DOT confirmed only <c>opId</c> varies per wallet, nothing
/// else in the request/auth shape.
/// </summary>
public enum DotFawryWalletOperator
{
    Fawry = 141,
    OrangeCash = 117,
    VodafoneCash = 114,
}

public static class DotFawryWalletOperatorExtensions
{
    /// <summary>The string form of this wallet's <c>opId</c>, as persisted on <c>PaymentAttempt.OperatorId</c> and sent on the wire.</summary>
    public static string ToOperatorId(this DotFawryWalletOperator wallet) =>
        ((int)wallet).ToString(CultureInfo.InvariantCulture);

    /// <summary>Parses a <c>PaymentAttempt.OperatorId</c> string back into the wallet it came from — the inverse of <see cref="ToOperatorId"/>.</summary>
    public static bool TryParseOperatorId(string? operatorId, out DotFawryWalletOperator wallet)
    {
        if (int.TryParse(operatorId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var opId)
            && Enum.IsDefined(typeof(DotFawryWalletOperator), opId))
        {
            wallet = (DotFawryWalletOperator)opId;
            return true;
        }

        wallet = default;
        return false;
    }
}
