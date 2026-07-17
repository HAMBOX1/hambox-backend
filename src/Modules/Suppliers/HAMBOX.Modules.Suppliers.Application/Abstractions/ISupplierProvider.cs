namespace HAMBOX.Modules.Suppliers.Application.Abstractions;

/// <summary>
/// The one contract every supplier integration implements. The marketplace core (Commerce/Catalog)
/// never references a concrete supplier — it resolves an instance via <see cref="ISupplierProviderRegistry"/>
/// and calls only these members. Nothing here assumes REST/HTTP: a future GraphQL, SOAP, CSV-drop, or
/// FTP-polling provider implements the exact same interface, so adding it never touches this contract
/// or any caller of it.
/// </summary>
public interface ISupplierProvider
{
    /// <summary>
    /// The key that <see cref="Domain.Suppliers.Supplier.ProviderType"/> must match for
    /// <see cref="ISupplierProviderRegistry"/> to resolve this instance (e.g. "Manual", future "Bamboo").
    /// </summary>
    string ProviderType { get; }

    Task<SupplierConnectionTestResult> TestConnectionAsync(
        SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierCredentialValidationResult> ValidateCredentialsAsync(
        SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierProductSyncResult> SyncProductsAsync(
        SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierInventorySyncResult> SyncInventoryAsync(
        SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierPriceSyncResult> SyncPricesAsync(
        SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierReservationResult> ReserveAsync(
        SupplierReservationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierPurchaseResult> PurchaseAsync(
        SupplierPurchaseRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierCancellationResult> CancelAsync(
        SupplierCancellationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierOrderStatusResult> GetOrderStatusAsync(
        string providerOrderId, SupplierProviderContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Everything a provider needs to act on behalf of one <c>Supplier</c> record, assembled by the
/// Application layer from decrypted entity data. Providers never receive a DbContext or the entity
/// itself — this is the seam that keeps provider implementations persistence-agnostic and testable.
/// </summary>
public sealed record SupplierProviderContext(
    Guid SupplierId,
    string SupplierCode,
    string? BaseUrl,
    SupplierProviderCredentials Credentials,
    string? SettingsJson);

public sealed record SupplierProviderCredentials(
    string? ApiKey,
    string? ApiSecret,
    string? Username,
    string? Password,
    string? BearerToken,
    string? OAuthSettingsJson);

public sealed record SupplierConnectionTestResult(bool IsSuccess, string Message);

public sealed record SupplierCredentialValidationResult(bool IsValid, string? Message);

public sealed record SupplierProductSyncResult(bool IsSuccess, int ProductsSynced, string? Message);

public sealed record SupplierInventorySyncResult(bool IsSuccess, int ItemsSynced, string? Message);

public sealed record SupplierPriceSyncResult(bool IsSuccess, int PricesSynced, string? Message);

public sealed record SupplierReservationRequest(string ExternalProductId, int Quantity, string? ReferenceId);

public sealed record SupplierReservationResult(bool IsSuccess, string? ProviderReservationId, string? Message);

public sealed record SupplierPurchaseRequest(
    string ExternalProductId, int Quantity, string? ProviderReservationId, string? ReferenceId);

public sealed record SupplierPurchaseResult(
    bool IsSuccess, string? ProviderOrderId, IReadOnlyCollection<string>? DeliveredCodes, string? Message);

public sealed record SupplierCancellationRequest(string ProviderOrderId, string? Reason);

public sealed record SupplierCancellationResult(bool IsSuccess, string? Message);

public sealed record SupplierOrderStatusResult(bool IsSuccess, string Status, string? Message);
