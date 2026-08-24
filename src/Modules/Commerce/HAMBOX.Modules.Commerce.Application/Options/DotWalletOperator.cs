using System.Globalization;

namespace HAMBOX.Modules.Commerce.Application.Options;

/// <summary>
/// The mobile wallets HAMBOX offers through DOT's Partners OTP Landing Page API (carrier-billing
/// consent flow) — a distinct DOT product from Direct Billing (<see cref="DotFawryWalletOperator"/>).
/// DOT confirmed Orange Cash and Vodafone Cash must go through this OTP redirect flow rather than
/// Direct Billing; Fawry stays on Direct Billing. The underlying enum value IS the wire-format
/// <c>op_id</c>. <see cref="DotSettings"/>'s shared credentials (<c>PartnerId</c>/<c>ServiceId</c>/
/// <c>Username</c>/<c>Password</c>) are identical across both wallets — only <c>op_id</c> varies.
/// </summary>
public enum DotWalletOperator
{
    OrangeCash = 117,
    VodafoneCash = 114,
}

public static class DotWalletOperatorExtensions
{
    /// <summary>The string form of this wallet's <c>op_id</c>, as persisted on <c>PaymentAttempt.OperatorId</c> and sent on the wire.</summary>
    public static string ToOperatorId(this DotWalletOperator wallet) =>
        ((int)wallet).ToString(CultureInfo.InvariantCulture);

    /// <summary>Parses a <c>PaymentAttempt.OperatorId</c> string back into the wallet it came from — the inverse of <see cref="ToOperatorId"/>.</summary>
    public static bool TryParseOperatorId(string? operatorId, out DotWalletOperator wallet)
    {
        if (int.TryParse(operatorId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var opId)
            && Enum.IsDefined(typeof(DotWalletOperator), opId))
        {
            wallet = (DotWalletOperator)opId;
            return true;
        }

        wallet = default;
        return false;
    }
}
