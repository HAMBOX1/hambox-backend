namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Marker/test-connection contract every payment gateway integration implements (currently
/// <c>CryptomusPaymentGateway</c>, <c>DotPaymentGateway</c>, <c>DotFawryPaymentGateway</c>). Resolved
/// by <see cref="GatewayKey"/> via DI's <c>IEnumerable&lt;IPaymentGateway&gt;</c> — a new gateway
/// registers itself the same way and needs no change anywhere else.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Must match the corresponding <c>PaymentGatewayConfiguration.GatewayKey</c> row (e.g. "cryptomus").</summary>
    string GatewayKey { get; }

    /// <summary>
    /// Makes the lightest real, authenticated call this provider supports to confirm the currently
    /// configured credentials actually work. Never throws — failures are reported via
    /// <see cref="PaymentGatewayTestResult.IsSuccess"/>/<see cref="PaymentGatewayTestResult.Message"/>.
    /// </summary>
    Task<PaymentGatewayTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed record PaymentGatewayTestResult(bool IsSuccess, string Message)
{
    public static PaymentGatewayTestResult Success(string message) => new(true, message);

    public static PaymentGatewayTestResult Failure(string message) => new(false, message);
}
