namespace HAMBOX.Modules.Commerce.Application.Contracts;

public sealed record PaymentGatewayListItemDto(
    string GatewayKey,
    string DisplayName,
    bool IsEnabled,
    bool IsTestMode,
    bool HasApiKey,
    bool HasApiSecret);

public sealed record PaymentGatewayDetailDto(
    string GatewayKey,
    string DisplayName,
    bool IsEnabled,
    bool IsTestMode,
    decimal? FeePercent,
    string? BaseUrl,
    string? AccountId,
    string? SecondaryId,
    bool HasApiKey,
    bool HasApiSecret,
    string? WebhookUrl,
    string? FrontendResultUrl,
    string? AdditionalConfigJson,
    DateTimeOffset ModifiedOnUtc);

public sealed record UpdatePaymentGatewayGeneralRequest(
    string DisplayName,
    bool IsTestMode,
    decimal? FeePercent,
    string? BaseUrl,
    string? AccountId,
    string? WebhookUrl,
    string? FrontendResultUrl,
    string? AdditionalConfigJson);

/// <summary>
/// A blank/omitted field here means "leave the stored value alone" — never "clear it". See
/// <c>PaymentGatewayConfiguration.UpdateCredentials</c>.
/// </summary>
public sealed record UpdatePaymentGatewayCredentialsRequest(string? ApiKey, string? ApiSecret, string? SecondaryId);

public sealed record PaymentGatewayTestConnectionResultDto(bool IsSuccess, string Message);
