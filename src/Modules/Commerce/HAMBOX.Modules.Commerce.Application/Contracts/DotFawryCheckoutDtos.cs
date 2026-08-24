namespace HAMBOX.Modules.Commerce.Application.Contracts;

/// <summary>
/// Returned from initiating a DOT Fawry checkout. <see cref="FawryReferenceNumber"/> is populated
/// only while the attempt is genuinely awaiting the customer's payment (resultCode "1000") — null if
/// the Direct Billing call already resolved (immediate success or failure), in which case the
/// frontend's status poll picks up the terminal state on its first tick. Its name predates the
/// Egyptian mobile wallet extension but the slot is shared by all three wallets (Fawry, Orange Cash,
/// Vodafone Cash) — see <see cref="Operator"/> for which one this attempt used.
/// </summary>
public sealed record DotFawryCheckoutInitiationDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string? FawryReferenceNumber,
    DateTimeOffset ExpiresOnUtc,
    string Operator);

/// <summary>
/// Customer-safe polling status for a DOT Fawry payment attempt. Deliberately excludes anything
/// provider-internal (raw DOT response fields, MSISDN). <see cref="Operator"/> names the wallet
/// selected at checkout (a <c>DotFawryWalletOperator</c> member name), so the frontend can show
/// wallet-specific copy without guessing.
/// </summary>
public sealed record DotFawryPaymentStatusDto(
    Guid PaymentAttemptId,
    Guid OrderId,
    string Status,
    string? FawryReferenceNumber,
    Guid? CompletedOrderId,
    string Operator);
